using System.Text.Json.Serialization;

namespace ClaimGuard.Domain.Events;

/// <summary>Published to the Kafka topic `claim.submitted` when a claim is created.</summary>
public record ClaimSubmittedEvent(
    [property: JsonPropertyName("claim_id")] Guid ClaimId,
    [property: JsonPropertyName("claim_number")] string ClaimNumber,
    [property: JsonPropertyName("policy_id")] Guid PolicyId,
    [property: JsonPropertyName("claimed_amount")] decimal ClaimedAmount,
    [property: JsonPropertyName("document_url")] string DocumentUrl,
    [property: JsonPropertyName("incident_date")] DateTime IncidentDate,
    [property: JsonPropertyName("submitted_at")] DateTime SubmittedAt);

/// <summary>Published to the Kafka topic `claim.scored` after AI triage completes.</summary>
public record ClaimScoredEvent(
    [property: JsonPropertyName("claim_id")] Guid ClaimId,
    [property: JsonPropertyName("claim_number")] string ClaimNumber,
    [property: JsonPropertyName("risk_score")] int RiskScore,
    [property: JsonPropertyName("risk_band")] string RiskBand,
    [property: JsonPropertyName("new_status")] string NewStatus,
    [property: JsonPropertyName("scored_at")] DateTime ScoredAt);

/// <summary>Published to the Kafka topic `claim.settled` when an adjudicator decides.</summary>
public record ClaimSettledEvent(
    [property: JsonPropertyName("claim_id")] Guid ClaimId,
    [property: JsonPropertyName("claim_number")] string ClaimNumber,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("approved_amount")] decimal? ApprovedAmount,
    [property: JsonPropertyName("decided_at")] DateTime DecidedAt);
