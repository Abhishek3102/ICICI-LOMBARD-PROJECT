using System.Text.Json;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using ClaimGuard.Domain.Events;
using ClaimGuard.Infrastructure.Options;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimGuard.Infrastructure.Messaging;

/// <summary>
/// AI Triage consumer. Reads `claim.submitted`, scores the claim with the AI engine,
/// writes the result + audit trail, and publishes `claim.scored`.
/// </summary>
public class KafkaConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaConsumerWorker> logger) : BackgroundService
{
    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield before the blocking Consume loop: ExecuteAsync runs synchronously
        // until its first await, and StartAsync would otherwise block host startup
        // (Kestrel included) until a message arrived — deadlocking the API.
        await Task.Yield();

        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.BootstrapServers))
        {
            logger.LogWarning("[v0] Kafka not configured; AI triage consumer is idle.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = cfg.BootstrapServers,
            GroupId = cfg.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SecurityProtocol = string.IsNullOrWhiteSpace(cfg.SaslUsername)
                ? SecurityProtocol.Plaintext
                : SecurityProtocol.SaslSsl,
            SaslMechanism = string.IsNullOrWhiteSpace(cfg.SaslUsername)
                ? SaslMechanism.Plain
                : TryParseMechanism(cfg.SaslMechanism, out var mechanism)
                    ? mechanism
                    : SaslMechanism.ScramSha256,
            SaslUsername = cfg.SaslUsername,
            SaslPassword = cfg.SaslPassword,

            // Home networks/VPNs silently drop long-lived TCP connections,
            // which shows up as session/heartbeat timeouts after several
            // minutes. Keepalive and a shorter idle window make the client
            // detect and replace dead connections quickly.
            SocketKeepaliveEnable = true,
            ConnectionsMaxIdleMs = 180_000,
            SessionTimeoutMs = 30_000,
            HeartbeatIntervalMs = 9_000
        };

        // Diagnostic: log the effective username and password LENGTH (never the
        // secret) so truncated or mismatched .env values are easy to spot.
        logger.LogInformation(
            "[v0] Kafka config: bootstrap={Bootstrap}, user={User}, passwordLength={PasswordLength}, mechanism={Mechanism}",
            cfg.BootstrapServers, cfg.SaslUsername,
            string.IsNullOrEmpty(cfg.SaslPassword) ? -1 : cfg.SaslPassword.Length,
            string.IsNullOrWhiteSpace(cfg.SaslUsername)
                ? "none"
                : TryParseMechanism(cfg.SaslMechanism, out var diagMech)
                    ? diagMech.ToString()
                    : "ScramSha256");

        // Aiven signs broker certificates with its own CA. Pin the CA when
        // certs/ca.pem exists (repo root or the API project folder); otherwise
        // skip verification so local runs still connect, with a loud warning.
        var caPath = new[]
        {
            "certs/ca.pem",
            Path.Combine("backend", "src", "ClaimGuard.Api", "certs", "ca.pem"),
            Path.Combine(AppContext.BaseDirectory, "certs", "ca.pem")
        }.FirstOrDefault(File.Exists);
        if (caPath is not null)
        {
            config.SslCaLocation = Path.GetFullPath(caPath);
            logger.LogInformation("[v0] Kafka TLS pinned to CA certificate {CaPath}", Path.GetFullPath(caPath));
        }
        else
        {
            config.SslCaLocation = null;
            config.EnableSslCertificateVerification = false;
            logger.LogWarning(
                "[v0] certs/ca.pem not found - Kafka TLS certificate verification is DISABLED. " +
                "Download the CA certificate from the Aiven console and save it as backend/src/ClaimGuard.Api/certs/ca.pem to fix this.");
        }

        // A broken Kafka setup must never take the whole API down; degrade to idle.
        try
        {
            _consumer = new ConsumerBuilder<string, string>(config).Build();
            _consumer.Subscribe(KafkaTopics.ClaimSubmitted);
            logger.LogInformation("[v0] AI triage consumer subscribed to {Topic}", KafkaTopics.ClaimSubmitted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[v0] Kafka consumer failed to start; AI triage is idle. Check the Kafka settings in .env.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Confluent.Kafka's IConsumer has no async Consume; the blocking call is
                // acceptable inside a dedicated BackgroundService loop.
                var result = _consumer.Consume(stoppingToken);
                using var scope = scopeFactory.CreateScope();
                await ProcessClaimAsync(result.Message.Value, result.TopicPartitionOffset.ToString(), scope.ServiceProvider, stoppingToken);
                _consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "[v0] Kafka consume error: {Reason}", ex.Error.Reason);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[v0] Unexpected error in triage loop; retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _consumer.Close();
    }

    // Accepts both .NET enum names (ScramSha512) and the Kafka-standard wire
    // strings ("SCRAM-SHA-512", "scram-sha-512") by stripping separators first.
    private static bool TryParseMechanism(string? value, out SaslMechanism mechanism) =>
        Enum.TryParse(
            value?.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty),
            ignoreCase: true,
            out mechanism);

    private static async Task ProcessClaimAsync(string messageJson, string kafkaOffset, IServiceProvider services, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<ClaimSubmittedEvent>(messageJson);
        if (evt is null) return;

        var claims = services.GetRequiredService<IClaimRepository>();
        var policies = services.GetRequiredService<IPolicyRepository>();
        var audits = services.GetRequiredService<IAuditRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var scorer = services.GetRequiredService<IAiScoringService>();
        var eventBus = services.GetRequiredService<IEventBus>();
        var taskQueue = services.GetRequiredService<ITaskQueue>();
        var logger = services.GetRequiredService<ILogger<KafkaConsumerWorker>>();

        var claim = await claims.GetByIdAsync(evt.ClaimId, ct);
        if (claim is null)
        {
            logger.LogWarning("[v0] Claim {ClaimId} from Kafka not found in DB; skipping.", evt.ClaimId);
            return;
        }

        var policy = await policies.GetByIdAsync(claim.PolicyId, ct);
        if (policy is null) return;

        var score = await scorer.ScoreClaimAsync(claim, policy, ct);

        claim.AiRiskScore = score.Score;
        claim.AiRiskBand = score.Band;
        claim.AiReasoning = JsonSerializer.Serialize(score, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // Auto-approve only low-risk claims under the ₹10,000 fast-track threshold.
        claim.Status = score.Band == RiskBand.Low && claim.ClaimedAmount <= 10_000m
            ? ClaimStatus.AutoApproved
            : ClaimStatus.ManualReview;
        claim.UpdatedAt = DateTime.UtcNow;

        await audits.AddAsync(new ClaimAuditLog
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ActorType = ActorType.SystemAi,
            ActorId = null,
            PreviousStatus = ClaimStatus.Submitted.ToString().ToUpperInvariant(),
            NewStatus = claim.Status.ToString().ToUpperInvariant(),
            Remarks = $"AI triage ({score.Engine}): score {score.Score} ({score.Band.ToString().ToUpperInvariant()}). {score.Reasoning}",
            KafkaOffset = kafkaOffset,
            CreatedAt = DateTime.UtcNow
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(KafkaTopics.ClaimScored, new ClaimScoredEvent(
            claim.Id, claim.ClaimNumber, score.Score, score.Band.ToString().ToUpperInvariant(),
            claim.Status.ToString().ToUpperInvariant(), claim.UpdatedAt), ct);

        await taskQueue.EnqueueNotificationAsync(new NotificationTask(
            Channel: "email",
            Recipient: policy.Holder?.Email ?? "policyholder@claimguard.local",
            Subject: $"Claim {claim.ClaimNumber} update",
            Body: claim.Status == ClaimStatus.AutoApproved
                ? $"Your claim {claim.ClaimNumber} passed automated verification and is approved for {claim.ClaimedAmount:N0}."
                : $"Your claim {claim.ClaimNumber} is under review by our claims team.",
            ClaimNumber: claim.ClaimNumber), ct);

        logger.LogInformation("[v0] Claim {ClaimNumber} scored {Score} ({Band}) -> {Status}",
            claim.ClaimNumber, score.Score, score.Band, claim.Status);
    }
}
