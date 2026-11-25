using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Domain.Entities;

/// <summary>
/// Immutable regulatory and AI audit trail (IRDAI compliance).
/// Every status transition appends a row; rows are never updated or deleted.
/// </summary>
public class ClaimAuditLog
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public AppUser? Actor { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    /// <summary>Links the DB record to the Kafka event stream offset for replay.</summary>
    public string? KafkaOffset { get; set; }

    public DateTime CreatedAt { get; set; }
}
