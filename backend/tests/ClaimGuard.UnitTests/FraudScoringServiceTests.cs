using ClaimGuard.Application.Services;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using Xunit;

namespace ClaimGuard.UnitTests;

public class FraudScoringServiceTests
{
    private readonly FraudScoringService _sut = new();

    private static Policy ActivePolicy(decimal sumInsured = 500_000m, decimal balance = 500_000m) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = "POL-TEST-001",
        PolicyType = PolicyType.Health,
        SumInsured = sumInsured,
        RemainingBalance = balance,
        Status = PolicyStatus.Active,
        ValidFrom = DateTime.UtcNow.AddDays(-180),
        ValidTo = DateTime.UtcNow.AddDays(180)
    };

    private static Claim NormalClaim(decimal amount = 5_000m) => new()
    {
        Id = Guid.NewGuid(),
        ClaimNumber = "CLM-TEST-0001",
        ClaimedAmount = amount,
        IncidentDate = DateTime.UtcNow.AddDays(-10),
        DocumentUrl = "https://docs.example.com/invoice.pdf"
    };

    [Fact]
    public void Score_NormalClaim_ReturnsLowBand()
    {
        var result = _sut.Score(NormalClaim(), ActivePolicy());

        Assert.Equal(RiskBand.Low, result.Band);
        Assert.True(result.Score <= 34);
        Assert.Equal("deterministic-rules-v1", result.Engine);
    }

    [Fact]
    public void Score_AmountExceedsBalance_FlagsAnomalyAndRaisesScore()
    {
        var result = _sut.Score(NormalClaim(600_000m), ActivePolicy(balance: 500_000m));

        Assert.Contains(result.Anomalies, a => a.Contains("remaining policy balance"));
        Assert.True(result.Score >= 50);
    }

    [Fact]
    public void Score_EarlyClaimAfterInception_IsFlagged()
    {
        var policy = ActivePolicy();
        policy.ValidFrom = DateTime.UtcNow.AddDays(-3);
        var claim = NormalClaim();
        claim.IncidentDate = DateTime.UtcNow.AddDays(-1);

        var result = _sut.Score(claim, policy);

        Assert.Contains(result.Anomalies, a => a.Contains("early-claim"));
    }

    [Fact]
    public void Score_LargeRoundNumber_AddsRoundNumberAnomaly()
    {
        var result = _sut.Score(NormalClaim(100_000m), ActivePolicy());

        Assert.Contains(result.Anomalies, a => a.Contains("round-number"));
    }

    [Fact]
    public void Score_NeverExceeds100()
    {
        var policy = ActivePolicy(sumInsured: 100_000m, balance: 10m);
        policy.ValidFrom = DateTime.UtcNow.AddDays(-1);
        var claim = NormalClaim(1_000_000m);
        claim.IncidentDate = DateTime.UtcNow.AddDays(1);

        var result = _sut.Score(claim, policy);

        Assert.True(result.Score is >= 0 and <= 100);
        Assert.Equal(RiskBand.High, result.Band);
    }
}
