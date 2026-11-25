using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using ClaimGuard.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimGuard.Infrastructure.AI;

/// <summary>
/// Gemini-powered document and fraud analyzer. Sends the claim context to the Gemini API,
/// expects structured JSON back, validates it, and falls back to the deterministic rule
/// engine on any failure (missing key, network error, malformed output).
/// </summary>
public class GeminiClaimsAnalyzer(
    HttpClient http,
    IOptions<GeminiOptions> options,
    FraudScoringService fallback,
    ILogger<GeminiClaimsAnalyzer> logger) : IAiScoringService
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiScoreResult> ScoreClaimAsync(Claim claim, Policy policy, CancellationToken ct = default)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            logger.LogInformation("[v0] GEMINI_API_KEY not set; using deterministic scoring engine.");
            return fallback.Score(claim, policy);
        }

        try
        {
            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = BuildPrompt(claim, policy) } } }
                },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.Model}:generateContent?key={cfg.ApiKey}";
            var response = await http.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return fallback.Score(claim, policy);

            var parsed = JsonSerializer.Deserialize<GeminiScorePayload>(text, PayloadOptions);
            if (parsed is null || parsed.Score is < 0 or > 100)
                return fallback.Score(claim, policy);

            var band = parsed.Band?.Trim().ToUpperInvariant() switch
            {
                "LOW" => RiskBand.Low,
                "HIGH" => RiskBand.High,
                _ => RiskBand.Medium
            };

            return new AiScoreResult(
                Score: parsed.Score.Value,
                Band: band,
                Confidence: parsed.Confidence ?? 0.7,
                Anomalies: parsed.Anomalies ?? Array.Empty<string>(),
                ExtractedItems: parsed.ExtractedItems ?? Array.Empty<ExtractedLineItem>(),
                Reasoning: string.IsNullOrWhiteSpace(parsed.Reasoning) ? "Gemini structured analysis." : parsed.Reasoning,
                Engine: $"gemini:{cfg.Model}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[v0] Gemini scoring failed; falling back to deterministic rules.");
            return fallback.Score(claim, policy);
        }
    }

    private static string BuildPrompt(Claim claim, Policy policy)
    {
        return $"""
            You are an insurance claims fraud-triage analyst for an Indian general insurer.
            Analyze this claim and respond ONLY with JSON matching:
            {{"score": <0-100 int>, "band": "<LOW|MEDIUM|HIGH>", "confidence": <0-1 float>,
              "anomalies": ["<string>"], "extracted_items": [{{"description": "<string>", "amount": <number>}}],
              "reasoning": "<one paragraph, explainable, audit-safe>"}}

            Policy type: {policy.PolicyType}
            Sum insured: {policy.SumInsured}
            Remaining balance: {policy.RemainingBalance}
            Policy valid: {policy.ValidFrom:yyyy-MM-dd} to {policy.ValidTo:yyyy-MM-dd}
            Claimed amount: {claim.ClaimedAmount}
            Incident date: {claim.IncidentDate:yyyy-MM-dd}
            Document URL: {claim.DocumentUrl}

            Flag: amounts exceeding coverage, early-claim-after-inception patterns, implausible
            dates, round-number estimates, and duplicate-invoice indicators. Be conservative:
            if the document cannot be inspected, rely on the numeric signals above.
            """;
    }

    private sealed class GeminiScorePayload
    {
        [JsonPropertyName("score")] public int? Score { get; set; }
        [JsonPropertyName("band")] public string? Band { get; set; }
        [JsonPropertyName("confidence")] public double? Confidence { get; set; }
        [JsonPropertyName("anomalies")] public List<string>? Anomalies { get; set; }
        [JsonPropertyName("extracted_items")] public List<ExtractedLineItem>? ExtractedItems { get; set; }
        [JsonPropertyName("reasoning")] public string? Reasoning { get; set; }
    }
}
