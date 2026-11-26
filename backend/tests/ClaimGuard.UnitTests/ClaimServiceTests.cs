using ClaimGuard.Application.Common;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using ClaimGuard.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ClaimGuard.UnitTests;

public class ClaimServiceTests
{
    private readonly Mock<IClaimRepository> _claims = new();
    private readonly Mock<IPolicyRepository> _policies = new();
    private readonly Mock<IAuditRepository> _audits = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<ITaskQueue> _taskQueue = new();

    private ClaimService CreateSut() => new(
        _claims.Object, _policies.Object, _audits.Object, _unitOfWork.Object,
        _cache.Object, _eventBus.Object, _taskQueue.Object, NullLogger<ClaimService>.Instance);

    private static Policy ActivePolicy(Guid holderId) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = "POL-100",
        PolicyType = PolicyType.Health,
        SumInsured = 500_000m,
        RemainingBalance = 500_000m,
        Status = PolicyStatus.Active,
        ValidFrom = DateTime.UtcNow.AddDays(-180),
        ValidTo = DateTime.UtcNow.AddDays(180),
        HolderId = holderId,
        Holder = new AppUser { Id = holderId, Email = "holder@test.in", FullName = "Test Holder", Role = UserRole.Policyholder }
    };

    private static SubmitClaimRequest ValidRequest(Guid policyId) => new()
    {
        PolicyId = policyId,
        IncidentDate = DateTime.UtcNow.AddDays(-5),
        ClaimedAmount = 5_000m,
        DocumentUrl = "https://storage.example.com/invoice.pdf",
        IdempotencyKey = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task Submit_ValidClaim_PersistsAuditsAndPublishesEvent()
    {
        var holderId = Guid.NewGuid();
        var policy = ActivePolicy(holderId);
        _cache.Setup(c => c.AcquireIdempotencyLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _policies.Setup(p => p.GetByIdAsync(policy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        var sut = CreateSut();
        var result = await sut.SubmitClaimAsync(ValidRequest(policy.Id), holderId, UserRole.Policyholder);

        Assert.Equal(ClaimStatus.Submitted.ToString().ToUpperInvariant(), result.Status);
        Assert.StartsWith("CLM-", result.ClaimNumber);
        _claims.Verify(r => r.AddAsync(It.IsAny<Claim>(), It.IsAny<CancellationToken>()), Times.Once);
        _audits.Verify(r => r.AddAsync(It.IsAny<ClaimAuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(e => e.PublishAsync(KafkaTopics.ClaimSubmitted, It.IsAny<ClaimSubmittedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _taskQueue.Verify(q => q.EnqueueNotificationAsync(It.IsAny<NotificationTask>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_DuplicateIdempotencyKey_ThrowsDuplicateClaimException()
    {
        _cache.Setup(c => c.AcquireIdempotencyLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _claims.Setup(c => c.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Claim?)null);

        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateClaimException>(() =>
            sut.SubmitClaimAsync(ValidRequest(Guid.NewGuid()), Guid.NewGuid(), UserRole.Policyholder));
    }

    [Fact]
    public async Task Submit_ClaimExceedingBalance_ThrowsValidation()
    {
        var holderId = Guid.NewGuid();
        var policy = ActivePolicy(holderId);
        _cache.Setup(c => c.AcquireIdempotencyLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _policies.Setup(p => p.GetByIdAsync(policy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        var request = ValidRequest(policy.Id) with { ClaimedAmount = 900_000m };
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sut.SubmitClaimAsync(request, holderId, UserRole.Policyholder));
        Assert.Contains("remaining balance", ex.Message);
    }

    [Fact]
    public async Task Submit_PolicyholderClaimingForeignPolicy_ThrowsForbidden()
    {
        _cache.Setup(c => c.AcquireIdempotencyLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _policies.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActivePolicy(Guid.NewGuid())); // owned by someone else

        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitClaimAsync(ValidRequest(Guid.NewGuid()), Guid.NewGuid(), UserRole.Policyholder));
    }
}
