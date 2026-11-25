using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Domain.Entities;

/// <summary>Maps to the claims table.</summary>
public class Claim
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid PolicyId { get; set; }
    public Policy? Policy { get; set; }
    public Guid SubmittedBy { get; set; }
    public AppUser? SubmittedByUser { get; set; }

    /// <summary>Unique. Backed by the Redis idempotency lock and a DB unique constraint.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime IncidentDate { get; set; }
    public decimal ClaimedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string DocumentUrl { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

    public int? AiRiskScore { get; set; }
    public RiskBand? AiRiskBand { get; set; }

    /// <summary>JSONB. Extracted line items, anomalies and model confidence.</summary>
    public string? AiReasoning { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
