namespace ClaimGuard.Application.Services;

/// <summary>Kafka topic names (created in Upstash, 1 partition each).</summary>
public static class KafkaTopics
{
    public const string ClaimSubmitted = "claim.submitted";
    public const string ClaimScored = "claim.scored";
    public const string ClaimSettled = "claim.settled";
}
