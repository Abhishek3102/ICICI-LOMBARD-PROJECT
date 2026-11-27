namespace ClaimGuard.Infrastructure.Options;

public class RedisOptions
{
    public const string SectionName = "Redis";
    public string Connection { get; set; } = string.Empty;
}

public class KafkaOptions
{
    public const string SectionName = "Kafka";
    public string BootstrapServers { get; set; } = string.Empty;
    public string SaslUsername { get; set; } = string.Empty;
    public string SaslPassword { get; set; } = string.Empty;
    // Optional: ScramSha256 (default) or ScramSha512, depending on what the
    // broker enables (Aiven: Kafka settings -> Authentication).
    public string SaslMechanism { get; set; } = string.Empty;
    public string GroupId { get; set; } = "claimguard-triage";
}

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string Url { get; set; } = string.Empty;
}

public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
}

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningSecret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "claimguard-api";
    public string Audience { get; set; } = "claimguard-clients";
    public int ExpiryMinutes { get; set; } = 480;
}

public class SupabaseOptions
{
    public const string SectionName = "Supabase";
    public string Url { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string DocumentsBucket { get; set; } = "claim-documents";
}
