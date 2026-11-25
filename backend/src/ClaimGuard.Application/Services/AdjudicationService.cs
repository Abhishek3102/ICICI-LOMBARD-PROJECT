using ClaimGuard.Application.Common;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using ClaimGuard.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ClaimGuard.Application.Services;

public class AdjudicationService(
    IClaimRepository claims,
    IAuditRepository audits,
    IUnitOfWork unitOfWork,
    IEventBus eventBus,
    ITaskQueue taskQueue,
    ILogger<AdjudicationService> logger)
{
    public async Task<IReadOnlyList<ReviewQueueItem>> GetReviewQueueAsync(ClaimStatus status, int limit = 50, CancellationToken ct = default)
    {
        var list = await claims.GetReviewQueueAsync(status, limit, ct);
        return list.Select(c => new ReviewQueueItem(
            c.Id,
            c.ClaimNumber,
            c.Policy!.PolicyNumber,
            c.Policy.PolicyType.ToString().ToUpperInvariant(),
            c.Policy.Holder?.FullName ?? "Unknown",
            c.ClaimedAmount,
            c.Policy.RemainingBalance,
            c.AiRiskScore,
            c.AiRiskBand?.ToString().ToUpperInvariant(),
            c.AiReasoning,
            c.DocumentUrl,
            c.CreatedAt)).ToList();
    }

    public async Task<ClaimResponse> DecideAsync(AdjudicationDecisionRequest request, Guid adjudicatorId, CancellationToken ct = default)
    {
        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("APPROVED" or "REJECTED"))
            throw new ValidationException("Decision must be APPROVED or REJECTED.");

        var claim = await claims.GetByIdWithDetailsAsync(request.ClaimId, ct)
            ?? throw new NotFoundException($"Claim {request.ClaimId} was not found.");

        if (claim.Status != ClaimStatus.ManualReview)
            throw new ValidationException($"Claim {claim.ClaimNumber} is not awaiting manual review (current status: {claim.Status.ToString().ToUpperInvariant()}).");

        var previousStatus = claim.Status.ToString().ToUpperInvariant();
        var policy = claim.Policy!;

        if (decision == "APPROVED")
        {
            var amount = request.ApprovedAmount ?? claim.ClaimedAmount;
            if (amount > claim.ClaimedAmount)
                throw new ValidationException("Approved amount cannot exceed the claimed amount.");
            if (amount > policy.RemainingBalance)
                throw new ValidationException("Approved amount cannot exceed the policy remaining balance.");

            claim.ApprovedAmount = amount;
            claim.Status = ClaimStatus.Approved;
            policy.RemainingBalance -= amount;
        }
        else
        {
            claim.Status = ClaimStatus.Rejected;
        }

        claim.UpdatedAt = DateTime.UtcNow;

        await audits.AddAsync(new ClaimAuditLog
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ActorType = ActorType.Adjudicator,
            ActorId = adjudicatorId,
            PreviousStatus = previousStatus,
            NewStatus = claim.Status.ToString().ToUpperInvariant(),
            Remarks = $"Reason code: {request.ReasonCode}. {request.Remarks}".Trim(),
            CreatedAt = DateTime.UtcNow
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(KafkaTopics.ClaimSettled, new ClaimSettledEvent(
            claim.Id, claim.ClaimNumber, decision, claim.ApprovedAmount, claim.UpdatedAt), ct);

        await taskQueue.EnqueueNotificationAsync(new NotificationTask(
            Channel: "sms",
            Recipient: policy.Holder?.Email ?? "policyholder@claimguard.local",
            Subject: $"Claim {claim.ClaimNumber} {decision.ToLowerInvariant()}",
            Body: decision == "APPROVED"
                ? $"Good news! Claim {claim.ClaimNumber} has been approved for {claim.ApprovedAmount:N0}."
                : $"Claim {claim.ClaimNumber} was rejected. Reason: {request.ReasonCode}.",
            ClaimNumber: claim.ClaimNumber), ct);

        logger.LogInformation("[v0] Claim {ClaimNumber} decided: {Decision} by {Adjudicator}",
            claim.ClaimNumber, decision, adjudicatorId);

        return ClaimService.MapToResponse(claim, policy);
    }
}
