using ClaimGuard.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "ADJUDICATOR,ADMIN")]
public class AuditController(IAuditRepository audits) : ControllerBase
{
    /// <summary>Recent AI + human decision trail across all claims (IRDAI governance view).</summary>
    [HttpGet("logs")]
    public async Task<IActionResult> Recent([FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var logs = await audits.GetRecentAsync(Math.Clamp(limit, 1, 500), ct);
        return Ok(logs.Select(a => new
        {
            a.Id,
            a.ClaimId,
            ClaimNumber = a.Claim?.ClaimNumber,
            ActorType = a.ActorType.ToString().ToUpperInvariant(),
            ActorName = a.Actor?.FullName,
            a.PreviousStatus,
            a.NewStatus,
            a.Remarks,
            a.KafkaOffset,
            a.CreatedAt
        }));
    }

    /// <summary>Complete audit transcript for one claim.</summary>
    [HttpGet("claims/{id:guid}/logs")]
    public async Task<IActionResult> ForClaim(Guid id, CancellationToken ct)
    {
        var logs = await audits.GetByClaimIdAsync(id, ct);
        return Ok(logs.Select(a => new
        {
            a.Id,
            ActorType = a.ActorType.ToString().ToUpperInvariant(),
            ActorName = a.Actor?.FullName,
            a.PreviousStatus,
            a.NewStatus,
            a.Remarks,
            a.KafkaOffset,
            a.CreatedAt
        }));
    }
}
