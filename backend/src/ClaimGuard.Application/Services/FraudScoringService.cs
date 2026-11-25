using ClaimGuard.Application.DTOs;
using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Application.Services;

/// <summary>
/// Deterministic, fully explainable fraud scoring rules. Used as the fallback engine when the
/// Gemini LLM call fails, and as the baseline every AI result is validated against.
/// </summary>
public class FraudScoringService
{
    public AiScoreResult Score(Domain.Entities.Claim claim, Domain.Entities.Policy policy)
    {
        var score = 10;
        var anomalies = new List<string>();

        if (claim.ClaimedAmount > policy.RemainingBalance)
        {
            score += 40;
            anomalies.Add($"Claimed amount {claim.ClaimedAmount:N0} exceeds remaining policy balance {policy.RemainingBalance:N0}.");
        }

        if (claim.ClaimedAmount > policy.SumInsured * 0.80m)
        {
            score += 25;
            anomalies.Add($"Claimed amount is above 80% of the sum insured ({policy.SumInsured:N0}).");
        }

        var daysSincePolicyStart = (claim.IncidentDate.Date - policy.ValidFrom.Date).TotalDays;
        if (daysSincePolicyStart is >= 0 and <= 7)
        {
            score += 20;
            anomalies.Add($"Incident occurred only {Math.Max(0, (int)daysSincePolicyStart)} day(s) after policy inception (early-claim pattern).");
        }

        var ageOfIncident = (DateTime.UtcNow.Date - claim.IncidentDate.Date).TotalDays;
        if (claim.IncidentDate.Date > DateTime.UtcNow.Date || ageOfIncident > 365)
        {
            score += 15;
            anomalies.Add("Incident date is in the future or older than 365 days.");
        }

        if (claim.ClaimedAmount >= 50_000m && claim.ClaimedAmount % 10_000m == 0)
        {
            score += 10;
            anomalies.Add("Large round-number claim amount (manual estimate pattern).");
        }

        score = Math.Clamp(score, 0, 100);
        var band = score switch
        {
            <= 34 => RiskBand.Low,
            <= 69 => RiskBand.Medium,
            _ => RiskBand.High
        };

        var reasoning = band == RiskBand.Low
            ? "Deterministic rules found no billing anomalies."
            : $"Deterministic rules flagged {anomalies.Count} anomaly pattern(s).";

        return new AiScoreResult(
            Score: score,
            Band: band,
            Confidence: 0.85,
            Anomalies: anomalies,
            ExtractedItems: Array.Empty<ExtractedLineItem>(),
            Reasoning: reasoning,
            Engine: "deterministic-rules-v1");
    }
}
