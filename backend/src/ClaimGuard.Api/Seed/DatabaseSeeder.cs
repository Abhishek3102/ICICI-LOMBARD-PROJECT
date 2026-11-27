using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using ClaimGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimGuard.Api.Seed;

/// <summary>
/// Idempotent demo-data seeder. Runs once at startup; skips entirely when the
/// marker user already exists, so it never duplicates rows or overwrites real
/// registrations. Failures are logged and swallowed so a database hiccup can
/// never block API startup.
/// </summary>
public class DatabaseSeeder(IServiceScopeFactory scopeFactory, ILogger<DatabaseSeeder> logger) : IHostedService
{
    public const string MarkerEmail = "adjudicator@claimguard.dev";
    public const string DemoPassword = "Test@1234";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            if (await db.Users.AnyAsync(u => u.Email == MarkerEmail, cancellationToken))
            {
                logger.LogInformation("[v0] Seed data already present; skipping seeder.");
                return;
            }

            var hash = hasher.Hash(DemoPassword);
            var now = DateTime.UtcNow;

            var adjudicator = new AppUser
            {
                Id = Guid.NewGuid(), Email = MarkerEmail, FullName = "Priya Sharma",
                Role = UserRole.Adjudicator, PasswordHash = hash, CreatedAt = now
            };
            var partner = new AppUser
            {
                Id = Guid.NewGuid(), Email = "partner@claimguard.dev", FullName = "CityCare Hospital",
                Role = UserRole.Partner, PartnerGstin = "27AABCU9603R1ZX", PasswordHash = hash, CreatedAt = now
            };
            var alice = new AppUser
            {
                Id = Guid.NewGuid(), Email = "alice@claimguard.dev", FullName = "Alice Fernandes",
                Role = UserRole.Policyholder, PasswordHash = hash, CreatedAt = now
            };
            var bob = new AppUser
            {
                Id = Guid.NewGuid(), Email = "bob@claimguard.dev", FullName = "Bob Kulkarni",
                Role = UserRole.Policyholder, PasswordHash = hash, CreatedAt = now
            };

            db.Users.AddRange(adjudicator, partner, alice, bob);

            var healthAlice = new Policy
            {
                Id = Guid.NewGuid(), PolicyNumber = "POL-2026-HL-0001", HolderId = alice.Id,
                PolicyType = PolicyType.Health, SumInsured = 500000m, RemainingBalance = 435000m,
                Status = PolicyStatus.Active, ValidFrom = now.AddMonths(-8), ValidTo = now.AddMonths(4), CreatedAt = now
            };
            var motorAlice = new Policy
            {
                Id = Guid.NewGuid(), PolicyNumber = "POL-2026-MT-0002", HolderId = alice.Id,
                PolicyType = PolicyType.Motor, SumInsured = 850000m, RemainingBalance = 850000m,
                Status = PolicyStatus.Active, ValidFrom = now.AddMonths(-3), ValidTo = now.AddMonths(9), CreatedAt = now
            };
            var healthBob = new Policy
            {
                Id = Guid.NewGuid(), PolicyNumber = "POL-2026-HL-0003", HolderId = bob.Id,
                PolicyType = PolicyType.Health, SumInsured = 300000m, RemainingBalance = 180000m,
                Status = PolicyStatus.Active, ValidFrom = now.AddMonths(-14), ValidTo = now.AddMonths(-2), CreatedAt = now
            };
            var motorBob = new Policy
            {
                Id = Guid.NewGuid(), PolicyNumber = "POL-2025-MT-0004", HolderId = bob.Id,
                PolicyType = PolicyType.Motor, SumInsured = 1200000m, RemainingBalance = 950000m,
                Status = PolicyStatus.Active, ValidFrom = now.AddYears(-1), ValidTo = now.AddMonths(1), CreatedAt = now
            };

            db.Policies.AddRange(healthAlice, motorAlice, healthBob, motorBob);

            var claims = new List<Claim>
            {
                new()
                {
                    Id = Guid.NewGuid(), ClaimNumber = "CLM-2026-SEED01", PolicyId = healthAlice.Id, SubmittedBy = alice.Id,
                    IdempotencyKey = "seed-idem-0001", IncidentDate = now.AddDays(-6), ClaimedAmount = 65000m,
                    ApprovedAmount = 65000m, DocumentUrl = "https://example.com/docs/hospital-bill-001.pdf",
                    Status = ClaimStatus.AutoApproved, AiRiskScore = 12, AiRiskBand = RiskBand.Low,
                    AiReasoning = """{"anomalies":[],"confidence":0.94,"summary":"Bill matches tariff; no duplicates detected."}""",
                    CreatedAt = now.AddDays(-6), UpdatedAt = now.AddDays(-5)
                },
                new()
                {
                    Id = Guid.NewGuid(), ClaimNumber = "CLM-2026-SEED02", PolicyId = motorAlice.Id, SubmittedBy = alice.Id,
                    IdempotencyKey = "seed-idem-0002", IncidentDate = now.AddDays(-3), ClaimedAmount = 42000m,
                    DocumentUrl = "https://example.com/docs/garage-estimate-002.pdf",
                    Status = ClaimStatus.AiScoring, AiRiskScore = null, AiRiskBand = null, AiReasoning = null,
                    CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-3)
                },
                new()
                {
                    Id = Guid.NewGuid(), ClaimNumber = "CLM-2026-SEED03", PolicyId = healthBob.Id, SubmittedBy = bob.Id,
                    IdempotencyKey = "seed-idem-0003", IncidentDate = now.AddDays(-10), ClaimedAmount = 210000m,
                    DocumentUrl = "https://example.com/docs/surgery-bill-003.pdf",
                    Status = ClaimStatus.ManualReview, AiRiskScore = 68, AiRiskBand = RiskBand.Medium,
                    AiReasoning = """{"anomalies":["room-upgrade-not-covered"],"confidence":0.71,"summary":"ICU charges exceed policy sub-limit; manual adjudication required."}""",
                    CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-9)
                },
                new()
                {
                    Id = Guid.NewGuid(), ClaimNumber = "CLM-2026-SEED04", PolicyId = motorBob.Id, SubmittedBy = bob.Id,
                    IdempotencyKey = "seed-idem-0004", IncidentDate = now.AddDays(-20), ClaimedAmount = 310000m,
                    ApprovedAmount = 275000m, DocumentUrl = "https://example.com/docs/collision-repair-004.pdf",
                    Status = ClaimStatus.Approved, AiRiskScore = 35, AiRiskBand = RiskBand.Low,
                    AiReasoning = """{"anomalies":["depreciation-adjusted"],"confidence":0.88,"summary":"Approved with depreciation adjustment on parts."}""",
                    CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-17)
                },
                new()
                {
                    Id = Guid.NewGuid(), ClaimNumber = "CLM-2026-SEED05", PolicyId = healthBob.Id, SubmittedBy = bob.Id,
                    IdempotencyKey = "seed-idem-0005", IncidentDate = now.AddDays(-45), ClaimedAmount = 95000m,
                    ApprovedAmount = 0m, DocumentUrl = "https://example.com/docs/diagnostic-panel-005.pdf",
                    Status = ClaimStatus.Rejected, AiRiskScore = 91, AiRiskBand = RiskBand.High,
                    AiReasoning = """{"anomalies":["duplicate-invoice","provider-blacklisted"],"confidence":0.97,"summary":"Duplicate submission of an already settled claim."}""",
                    CreatedAt = now.AddDays(-45), UpdatedAt = now.AddDays(-43)
                },
                new()
                {
                    Id = Guid.NewGuid(), ClaimNumber = "CLM-2026-SEED06", PolicyId = healthAlice.Id, SubmittedBy = alice.Id,
                    IdempotencyKey = "seed-idem-0006", IncidentDate = now.AddDays(-1), ClaimedAmount = 18500m,
                    DocumentUrl = "https://example.com/docs/pharmacy-receipt-006.pdf",
                    Status = ClaimStatus.Submitted, AiRiskScore = null, AiRiskBand = null, AiReasoning = null,
                    CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1)
                }
            };

            db.Claims.AddRange(claims);

            db.AuditLogs.AddRange(
                new ClaimAuditLog
                {
                    Id = Guid.NewGuid(), ClaimId = claims[0].Id, ActorType = ActorType.SystemAi,
                    PreviousStatus = ClaimStatus.Submitted.ToString().ToUpperInvariant(),
                    NewStatus = ClaimStatus.AutoApproved.ToString().ToUpperInvariant(),
                    Remarks = "AI triage: risk 12 (LOW). Auto-approved within sum insured.", CreatedAt = now.AddDays(-5)
                },
                new ClaimAuditLog
                {
                    Id = Guid.NewGuid(), ClaimId = claims[2].Id, ActorType = ActorType.SystemAi,
                    PreviousStatus = ClaimStatus.Submitted.ToString().ToUpperInvariant(),
                    NewStatus = ClaimStatus.ManualReview.ToString().ToUpperInvariant(),
                    Remarks = "AI triage: risk 68 (MEDIUM). Routed to adjudicator queue.", CreatedAt = now.AddDays(-9)
                },
                new ClaimAuditLog
                {
                    Id = Guid.NewGuid(), ClaimId = claims[3].Id, ActorType = ActorType.Adjudicator, ActorId = adjudicator.Id,
                    PreviousStatus = ClaimStatus.ManualReview.ToString().ToUpperInvariant(),
                    NewStatus = ClaimStatus.Approved.ToString().ToUpperInvariant(),
                    Remarks = "Approved Rs. 275,000 after depreciation adjustment.", CreatedAt = now.AddDays(-17)
                },
                new ClaimAuditLog
                {
                    Id = Guid.NewGuid(), ClaimId = claims[4].Id, ActorType = ActorType.Adjudicator, ActorId = adjudicator.Id,
                    PreviousStatus = ClaimStatus.ManualReview.ToString().ToUpperInvariant(),
                    NewStatus = ClaimStatus.Rejected.ToString().ToUpperInvariant(),
                    Remarks = "Rejected: duplicate invoice, provider blacklisted.", CreatedAt = now.AddDays(-43)
                }
            );

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("[v0] Seeded demo data: 4 users, 4 policies, 6 claims, 4 audit logs. Demo logins use password '{Password}'.", DemoPassword);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown during startup; nothing to do.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[v0] Database seeding failed; API continues without demo data.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
