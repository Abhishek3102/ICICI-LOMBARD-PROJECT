using System.Text.Json.Serialization;

namespace ClaimGuard.Application.DTOs;

public record PolicyCoverageCache(
    [property: JsonPropertyName("policy_id")] Guid PolicyId,
    [property: JsonPropertyName("policy_type")] string PolicyType,
    [property: JsonPropertyName("sum_insured")] decimal SumInsured,
    [property: JsonPropertyName("remaining_balance")] decimal RemainingBalance,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("valid_to")] DateTime ValidTo);

public record NotificationTask(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("claim_number")] string? ClaimNumber);
