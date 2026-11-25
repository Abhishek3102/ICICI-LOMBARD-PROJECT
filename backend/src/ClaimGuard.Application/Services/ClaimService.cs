using ClaimGuard.Application.Common;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using ClaimGuard.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ClaimGuard.Application.Services;

public class ClaimService(
    IClaimRepository claims,
    IPolicyRepository policies,
    IAuditRepository audits,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    IEventBus eventBus,
    ITaskQueue taskQueue,
    ILogger<ClaimService> logger)
{
    public const string IdempotencyKeyPrefix = "claim:idem:";

    public async Task<ClaimResponse> SubmitClaimAsync(SubmitClaimRequest request, Guid submitterId, UserRole submitterRole, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ValidationException("Idempotency key is required.");

        // Redis idempotency lock: prevents duplicate payouts when a partner double-clicks.
        var lockKey = IdempotencyKeyPrefix + request.IdempotencyKey;
        var acquired = await cache.AcquireIdempotencyLockAsync(lockKey, TimeSpan.FromHours(24), ct);
        if (!acquired)
        {
            var existing = await claims.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
            var claimNumber = existing?.ClaimNumber ?? "in-flight";
            throw new DuplicateClaimException($"A claim with this idempotency key already exists ({claimNumber}).");
        }

        var policy = await policies.GetByIdAsync(request.PolicyId, ct)
            ?? throw new NotFoundException($"Policy {request.PolicyId} was not found.");

        if (policy.Status != PolicyStatus.Active)
            throw new ValidationException($"Policy {policy.PolicyNumber} is not active.");

        if (submitterRole == UserRole.Policyholder && policy.HolderId != submitterId)
            throw new ForbiddenException("You can only submit claims against your own policies.");

        if (request.ClaimedAmount > policy.RemainingBalance)
            throw new ValidationException(
                $"Claimed amount {request.ClaimedAmount:N0} exceeds the remaining balance {policy.RemainingBalance:N0} of policy {policy.PolicyNumber}.");

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = GenerateClaimNumber(),
            PolicyId = policy.Id,
            SubmittedBy = submitterId,
            IdempotencyKey = request.IdempotencyKey,
            IncidentDate = request.IncidentDate,
            ClaimedAmount = request.ClaimedAmount,
            DocumentUrl = request.DocumentUrl,
            Status = ClaimStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await claims.AddAsync(claim, ct);
        await audits.AddAsync(new ClaimAuditLog
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ActorType = submitterRole == UserRole.Partner ? ActorType.Partner : ActorType.Policyholder,
            ActorId = submitterId,
            PreviousStatus = null,
            NewStatus = ClaimStatus.Submitted.ToString().ToUpperInvariant(),
            Remarks = submitterRole == UserRole.Partner
                ? "Cashless pre-authorization submitted by network partner."
                : "Claim submitted by policyholder via self-service portal.",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(KafkaTopics.ClaimSubmitted, new ClaimSubmittedEvent(
            claim.Id, claim.ClaimNumber, policy.Id, claim.ClaimedAmount, claim.DocumentUrl,
            claim.IncidentDate, claim.CreatedAt), ct);

        await taskQueue.EnqueueNotificationAsync(new NotificationTask(
            Channel: "email",
            Recipient: policy.Holder?.Email ?? "policyholder@claimguard.local",
            Subject: $"Claim {claim.ClaimNumber} received",
            Body: $"Your claim for {claim.ClaimedAmount:N0} has been received and is being processed by our AI triage engine.",
            ClaimNumber: claim.ClaimNumber), ct);

        logger.LogInformation("[v0] Claim {ClaimNumber} submitted (policy {PolicyNumber}, amount {Amount})",
            claim.ClaimNumber, policy.PolicyNumber, claim.ClaimedAmount);

        return MapToResponse(claim, policy);
    }

    public async Task<ClaimResponse> GetClaimAsync(Guid claimId, Guid requesterId, UserRole requesterRole, CancellationToken ct = default)
    {
        var claim = await claims.GetByIdWithDetailsAsync(claimId, ct)
            ?? throw new NotFoundException($"Claim {claimId} was not found.");

        if (requesterRole == UserRole.Policyholder && claim.Policy!.HolderId != requesterId)
            throw new ForbiddenException("You can only view claims on your own policies.");

        return MapToResponse(claim, claim.Policy!);
    }

    public async Task<IReadOnlyList<ClaimResponse>> GetMyClaimsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await claims.GetBySubmitterAsync(userId, ct);
        return list.Select(c => MapToResponse(c, c.Policy!)).ToList();
    }

    public static string GenerateClaimNumber()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"CLM-{DateTime.UtcNow:yyyy}-{suffix}";
    }

    public static ClaimResponse MapToResponse(Claim claim, Policy policy) => new(
        claim.Id,
        claim.ClaimNumber,
        policy.Id,
        policy.PolicyNumber,
        policy.PolicyType.ToString().ToUpperInvariant(),
        claim.ClaimedAmount,
        claim.ApprovedAmount,
        claim.DocumentUrl,
        claim.Status.ToString().ToUpperInvariant(),
        claim.AiRiskScore,
        claim.AiRiskBand?.ToString().ToUpperInvariant(),
        claim.AiReasoning,
        claim.CreatedAt);
}
