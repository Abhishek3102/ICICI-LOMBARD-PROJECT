using System.ComponentModel.DataAnnotations;
using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Application.DTOs;

public record SubmitClaimRequest
{
    [Required]
    public Guid PolicyId { get; init; }

    [Required]
    public DateTime IncidentDate { get; init; }

    [Required, Range(0.01, 99_999_999)]
    public decimal ClaimedAmount { get; init; }

    [Required, Url]
    public string DocumentUrl { get; init; } = string.Empty;

    /// <summary>Client-generated unique key (UUID). Enforced by middleware, Redis lock and DB constraint.</summary>
    [Required, StringLength(100)]
    public string IdempotencyKey { get; init; } = string.Empty;
}

public record ClaimResponse(
    Guid Id,
    string ClaimNumber,
    Guid PolicyId,
    string PolicyNumber,
    string PolicyType,
    decimal ClaimedAmount,
    decimal? ApprovedAmount,
    string DocumentUrl,
    string Status,
    int? AiRiskScore,
    string? AiRiskBand,
    string? AiReasoning,
    DateTime CreatedAt);

public record AdjudicationDecisionRequest
{
    [Required]
    public Guid ClaimId { get; init; }

    /// <summary>APPROVED or REJECTED.</summary>
    [Required, StringLength(20)]
    public string Decision { get; init; } = string.Empty;

    /// <summary>Required when APPROVED. Cannot exceed the claimed amount or the remaining balance.</summary>
    [Range(0.01, 99_999_999)]
    public decimal? ApprovedAmount { get; init; }

    /// <summary>Mandatory reason code, logged for regulatory audit (e.g. DUPLICATE_INVOICE, OUT_OF_COVERAGE).</summary>
    [Required, StringLength(60)]
    public string ReasonCode { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Remarks { get; init; }
}

public record ReviewQueueItem(
    Guid Id,
    string ClaimNumber,
    string PolicyNumber,
    string PolicyType,
    string HolderName,
    decimal ClaimedAmount,
    decimal RemainingBalance,
    int? AiRiskScore,
    string? AiRiskBand,
    string? AiReasoning,
    string DocumentUrl,
    DateTime CreatedAt);

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;
}

public record RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    /// <summary>POLICYHOLDER (self signup) or PARTNER. ADJUDICATOR/ADMIN are seeded by SQL only.</summary>
    [Required]
    public string Role { get; init; } = "POLICYHOLDER";

    /// <summary>Required when Role is PARTNER.</summary>
    [StringLength(20)]
    public string? PartnerGstin { get; init; }

    /// <summary>
    /// Only needed when registering ADJUDICATOR/ADMIN accounts. Must match the
    /// Seed:BootstrapSecret configuration value; never shipped to normal clients.
    /// </summary>
    [StringLength(128)]
    public string? BootstrapSecret { get; init; }
}

public record AuthResponse(
    string Token,
    Guid UserId,
    string Email,
    string FullName,
    string Role);

public record UploadDocumentResponse(
    string DocumentUrl,
    string Path);
