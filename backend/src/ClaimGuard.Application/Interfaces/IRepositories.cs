using ClaimGuard.Domain.Entities;

namespace ClaimGuard.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);

    /// <summary>Persists pending changes made through this repository.</summary>
    Task SaveAsync(CancellationToken ct = default);
}

public interface IPolicyRepository
{
    Task<Policy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Policy?> GetByNumberAsync(string policyNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Policy>> GetByHolderAsync(Guid holderId, CancellationToken ct = default);
}

public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Claim?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<Claim?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<Claim>> GetBySubmitterAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Claim>> GetReviewQueueAsync(ClaimStatus status, int limit, CancellationToken ct = default);
    Task AddAsync(Claim claim, CancellationToken ct = default);
}

public interface IAuditRepository
{
    Task AddAsync(ClaimAuditLog log, CancellationToken ct = default);
    Task<IReadOnlyList<ClaimAuditLog>> GetByClaimIdAsync(Guid claimId, CancellationToken ct = default);
    Task<IReadOnlyList<ClaimAuditLog>> GetRecentAsync(int limit, CancellationToken ct = default);
}
