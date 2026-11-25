using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Domain.Entities;

/// <summary>Maps to the policies table.</summary>
public class Policy
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid HolderId { get; set; }
    public AppUser? Holder { get; set; }
    public PolicyType PolicyType { get; set; }
    public decimal SumInsured { get; set; }
    public decimal RemainingBalance { get; set; }
    public PolicyStatus Status { get; set; } = PolicyStatus.Active;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Stored as VARCHAR in the policies.status column.</summary>
public enum PolicyStatus
{
    Active = 0,
    Expired = 1,
    Suspended = 2
}
