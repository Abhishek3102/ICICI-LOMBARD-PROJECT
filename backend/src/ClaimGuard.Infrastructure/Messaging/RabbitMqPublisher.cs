using System.Text;
using System.Text.Json;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Infrastructure.Options;
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

    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        var url = options.Value.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("[v0] RabbitMQ URL not configured; task queue is disabled.");
            return;
        }

        var factory = new ConnectionFactory { Uri = new Uri(url), AutomaticRecoveryEnabled = true };
        _connection = factory.CreateConnection("claimguard-api");
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);
        _channel.QueueDeclare(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(DeadLetterQueue, DeadLetterExchange, routingKey: Queue);

        _channel.ExchangeDeclare(Exchange, ExchangeType.Direct, durable: true);
        var args = new Dictionary<string, object> { { "x-dead-letter-exchange", DeadLetterExchange } };
        _channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false, arguments: args);
        _channel.QueueBind(Queue, Exchange, routingKey: Queue);
    }

    public Task EnqueueNotificationAsync(NotificationTask task, CancellationToken ct = default)
    {
        if (_channel is null) return Task.CompletedTask;

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(task));
        var props = _channel.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";

        _channel.BasicPublish(Exchange, routingKey: Queue, basicProperties: props, body: body);
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

        var factory = new ConnectionFactory { Uri = new Uri(url), AutomaticRecoveryEnabled = true };
        var connection = factory.CreateConnection("claimguard-notifications");
        var channel = connection.CreateModel();
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

        // Keep the background service alive until shutdown; the EventingBasicConsumer runs on its own threads.
        return Task.Run(async () =>
        {
            try { await Task.Delay(Timeout.Infinite, stoppingToken); }
            catch (OperationCanceledException) { }
            finally
            {
                channel.Dispose();
                connection.Dispose();
            }
        }, stoppingToken);
    }
}
