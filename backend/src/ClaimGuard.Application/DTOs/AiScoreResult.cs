using System.Text.Json.Serialization;
using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Application.DTOs;

public record ExtractedLineItem(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("amount")] decimal Amount);

/// <summary>Output of the AI triage engine, persisted into claims.ai_reasoning (JSONB).</summary>
public record AiScoreResult(
    int Score,
    RiskBand Band,
    double Confidence,
    IReadOnlyList<string> Anomalies,
    IReadOnlyList<ExtractedLineItem> ExtractedItems,
    string Reasoning,
    string Engine);
