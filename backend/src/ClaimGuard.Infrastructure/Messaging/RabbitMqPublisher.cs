using System.Text;
using System.Text.Json;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ClaimGuard.Infrastructure.Messaging;

/// <summary>
/// Reliable point-to-point task queue over CloudAMQP with a dead-letter exchange:
/// failed notification deliveries land in notifications.dlq for inspection and retry.
/// </summary>
public class RabbitMqPublisher : ITaskQueue, IDisposable
{
    public const string Exchange = "claimguard.notifications";
    public const string Queue = "notifications.q";
    public const string DeadLetterExchange = "claimguard.dlx";
    public const string DeadLetterQueue = "notifications.dlq";

    private readonly string? _url;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly object _connectLock = new();
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        _url = options.Value.Url;

        // The connection is opened lazily on first publish. Connecting in the
        // constructor runs during host startup (before Kestrel listens), so an
        // unreachable broker would hang the whole API on boot.
        if (string.IsNullOrWhiteSpace(_url))
            logger.LogWarning("[v0] RabbitMQ URL not configured; task queue is disabled.");
    }

    // Returns false instead of throwing when the broker is unreachable, so a
    // CloudAMQP outage degrades notifications without taking the API down.
    private bool TryConnect()
    {
        if (_channel is not null) return true;
        if (string.IsNullOrWhiteSpace(_url)) return false;

        lock (_connectLock)
        {
            if (_channel is not null) return true;
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_url),
                    AutomaticRecoveryEnabled = true,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
                };
                _connection = factory.CreateConnection("claimguard-api");
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);
                _channel.QueueDeclare(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(DeadLetterQueue, DeadLetterExchange, routingKey: Queue);

                _channel.ExchangeDeclare(Exchange, ExchangeType.Direct, durable: true);
                var args = new Dictionary<string, object> { { "x-dead-letter-exchange", DeadLetterExchange } };
                _channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false, arguments: args);
                _channel.QueueBind(Queue, Exchange, routingKey: Queue);

                _logger.LogInformation("[v0] RabbitMQ connected; task queue ready.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[v0] RabbitMQ connect failed; will retry on next notification.");
                _connection?.Dispose();
                _connection = null;
                return false;
            }
        }
    }

    public Task EnqueueNotificationAsync(NotificationTask task, CancellationToken ct = default)
    {
        if (!TryConnect())
        {
            _logger.LogWarning("[v0] Notification for {Recipient} dropped: RabbitMQ unavailable.", task.Recipient);
            return Task.CompletedTask;
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(task));
        var props = _channel!.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";

        _channel!.BasicPublish(Exchange, routingKey: Queue, basicProperties: props, body: body);
        _logger.LogDebug("[v0] Notification enqueued for {Recipient}", task.Recipient);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

/// <summary>
/// Consumes notification tasks and "sends" them (logged here; plug in an email/SMS provider).
/// Failures are nacked without requeue so they land in the dead-letter queue.
/// </summary>
public class NotificationWorker(
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = options.Value.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("[v0] RabbitMQ not configured; notification worker is idle.");
            return Task.CompletedTask;
        }

        // Connect in a retry loop instead of blocking startup: an unreachable
        // broker must never prevent the API from listening.
        return Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                IConnection? connection = null;
                IModel? channel = null;
                try
                {
                    var factory = new ConnectionFactory
                    {
                        Uri = new Uri(url),
                        AutomaticRecoveryEnabled = true,
                        RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
                    };
                    connection = factory.CreateConnection("claimguard-notifications");
                    channel = connection.CreateModel();
                    channel.BasicQos(0, prefetchCount: 10, global: false);

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (_, ea) =>
                    {
                        try
                        {
                            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                            var task = JsonSerializer.Deserialize<NotificationTask>(json);
                            // Replace with a real email/SMS provider integration.
                            logger.LogInformation("[v0] Notification [{Channel}] to {Recipient}: {Subject}",
                                task?.Channel, task?.Recipient, task?.Subject);
                            channel.BasicAck(ea.DeliveryTag, multiple: false);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[v0] Notification processing failed; routing to DLQ.");
                            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                        }
                    };

                    channel.BasicConsume(RabbitMqPublisher.Queue, autoAck: false, consumer);
                    logger.LogInformation("[v0] Notification worker consuming {Queue}", RabbitMqPublisher.Queue);

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[v0] RabbitMQ unavailable; notification worker retries in 30s.");
                    try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
                finally
                {
                    channel?.Dispose();
                    connection?.Dispose();
                }
            }
        }, stoppingToken);
    }
}
