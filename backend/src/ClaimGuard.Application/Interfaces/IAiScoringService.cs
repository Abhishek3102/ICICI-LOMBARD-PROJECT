using ClaimGuard.Application.DTOs;
using ClaimGuard.Domain.Entities;

namespace ClaimGuard.Application.Interfaces;

/// <summary>
/// AI triage engine. Extracts invoice / medical line items and computes a fraud risk score.
/// Implementations: GeminiClaimsAnalyzer (LLM) with a deterministic rule-based fallback.
/// </summary>
public interface IAiScoringService
{
    Task<AiScoreResult> ScoreClaimAsync(Claim claim, Policy policy, CancellationToken ct = default);
}
