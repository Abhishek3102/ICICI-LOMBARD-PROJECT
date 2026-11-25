using ClaimGuard.Domain.Entities;

namespace ClaimGuard.Application.Interfaces;

/// <summary>Redis-backed cache: idempotency locks, policy coverage cache, rate limiting.</summary>
public interface ICacheService
{
    /// <summary>SETNX-style lock. Returns false when the key already exists (duplicate submission).</summary>
    Task<bool> AcquireIdempotencyLockAsync(string key, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> HasIdempotencyLockAsync(string key, CancellationToken ct = default);

    Task<PolicyCoverageCache?> GetPolicyCacheAsync(Guid policyId, CancellationToken ct = default);

    Task SetPolicyCacheAsync(PolicyCoverageCache cache, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Fixed-window counter. Returns true when the caller exceeded maxRequests within the window.</summary>
    Task<bool> IsRateLimitedAsync(string clientKey, int maxRequests, TimeSpan window, CancellationToken ct = default);
}
