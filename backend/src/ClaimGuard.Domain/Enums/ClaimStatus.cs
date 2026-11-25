namespace ClaimGuard.Domain.Enums;

/// <summary>Stored as VARCHAR in the claims.status column.</summary>
public enum ClaimStatus
{
    Submitted = 0,
    AiScoring = 1,
    AutoApproved = 2,
    ManualReview = 3,
    Approved = 4,
    Rejected = 5
}
