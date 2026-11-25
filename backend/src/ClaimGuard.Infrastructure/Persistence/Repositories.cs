using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimGuard.Infrastructure.Persistence;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task SaveAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}

public class PolicyRepository(AppDbContext db) : IPolicyRepository
{
    public Task<Policy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Policies.Include(p => p.Holder).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Policy?> GetByNumberAsync(string policyNumber, CancellationToken ct = default) =>
        db.Policies.Include(p => p.Holder).FirstOrDefaultAsync(p => p.PolicyNumber == policyNumber, ct);

    public async Task<IReadOnlyList<Policy>> GetByHolderAsync(Guid holderId, CancellationToken ct = default)
    {
        var list = await db.Policies
            .Where(p => p.HolderId == holderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return list;
    }
}

public class ClaimRepository(AppDbContext db) : IClaimRepository
{
    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Claim?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        db.Claims
            .Include(c => c.Policy).ThenInclude(p => p!.Holder)
            .Include(c => c.SubmittedByUser)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Claim?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        db.Claims.FirstOrDefaultAsync(c => c.IdempotencyKey == idempotencyKey, ct);

    public async Task<IReadOnlyList<Claim>> GetBySubmitterAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await db.Claims
            .Include(c => c.Policy)
            .Where(c => c.SubmittedBy == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return list;
    }

    public async Task<IReadOnlyList<Claim>> GetReviewQueueAsync(ClaimStatus status, int limit, CancellationToken ct = default)
    {
        var list = await db.Claims
            .Include(c => c.Policy).ThenInclude(p => p!.Holder)
            .Where(c => c.Status == status)
            .OrderBy(c => c.AiRiskBand == RiskBand.High ? 0 : 1)
            .ThenByDescending(c => c.AiRiskScore)
            .ThenBy(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        return list;
    }

    public async Task AddAsync(Claim claim, CancellationToken ct = default) =>
        await db.Claims.AddAsync(claim, ct);
}

public class AuditRepository(AppDbContext db) : IAuditRepository
{
    public async Task AddAsync(ClaimAuditLog log, CancellationToken ct = default) =>
        await db.AuditLogs.AddAsync(log, ct);

    public async Task<IReadOnlyList<ClaimAuditLog>> GetByClaimIdAsync(Guid claimId, CancellationToken ct = default)
    {
        var list = await db.AuditLogs
            .Include(a => a.Actor)
            .Where(a => a.ClaimId == claimId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return list;
    }

    public async Task<IReadOnlyList<ClaimAuditLog>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var list = await db.AuditLogs
            .Include(a => a.Actor)
            .Include(a => a.Claim)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        return list;
    }
}
