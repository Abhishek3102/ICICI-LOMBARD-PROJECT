using System.Text.Json;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Infrastructure.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimGuard.Infrastructure.Messaging;

/// <summary>Low-level Kafka producer (Upstash, SASL SCRAM-SHA-256 over TLS).</summary>
public class KafkaProducer : IDisposable
{
    private readonly IProducer<string, string>? _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.BootstrapServers))
        {
            logger.LogWarning("[v0] Kafka BootstrapServers not configured; event publishing is disabled.");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = cfg.BootstrapServers,
            SecurityProtocol = string.IsNullOrWhiteSpace(cfg.SaslUsername)
                ? SecurityProtocol.Plaintext
                : SecurityProtocol.SaslSsl,
            SaslMechanism = string.IsNullOrWhiteSpace(cfg.SaslUsername)
                ? SaslMechanism.Plain
                // Normalize "SCRAM-SHA-512" style wire strings the same way the
                // consumer does, so both sides always agree on the mechanism.
                : Enum.TryParse(
                        cfg.SaslMechanism?.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty),
                        ignoreCase: true, out SaslMechanism prodMechanism)
                    ? prodMechanism
                    : SaslMechanism.ScramSha256,
            SaslUsername = cfg.SaslUsername,
            SaslPassword = cfg.SaslPassword,
            EnableIdempotence = true,
            Acks = Acks.All,

            // Match the consumer: survive NAT/firewall drops of long-lived
            // connections on home networks and VPNs.
            SocketKeepaliveEnable = true,
            ConnectionsMaxIdleMs = 180_000
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(string topic, object payload, CancellationToken ct = default)
    {
        if (_producer is null) return;

        var json = JsonSerializer.Serialize(payload);
        try
        {
            var result = await _producer.ProduceAsync(topic,
                new Message<string, string> { Key = Guid.NewGuid().ToString(), Value = json }, ct);
            _logger.LogInformation("[v0] Published to {Topic} at {Offset}", topic, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            // Never fail the business transaction because of broker availability; the audit
            // trail in PostgreSQL remains the source of truth.
            _logger.LogError(ex, "[v0] Failed to publish to {Topic}: {Reason}", topic, ex.Error.Reason);
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(5));
        _producer?.Dispose();
    }
}

/// <summary>IEventBus implementation backed by Kafka.</summary>
public class KafkaEventBus(KafkaProducer producer) : IEventBus
{
    public Task PublishAsync<TEvent>(string topic, TEvent evt, CancellationToken ct = default) where TEvent : class
        => producer.PublishAsync(topic, evt, ct);
}
