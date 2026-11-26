using ClaimGuard.Api.Extensions;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;
using ClaimGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
[Route("api/adjudication")]
[Authorize(Roles = "ADJUDICATOR,ADMIN")]
public class AdjudicationController(
    AdjudicationService adjudicationService,
    IClaimRepository claims) : ControllerBase
{
    /// <summary>
    /// The "1-Minute Co-Pilot" queue: claims flagged for manual review, highest risk first.
    /// </summary>
    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<ReviewQueueItem>>> Queue(
        [FromQuery] string? status, CancellationToken ct)
    {
        var parsed = Enum.TryParse<ClaimStatus>(status ?? "MANUAL_REVIEW", ignoreCase: true, out var s)
            ? s
            : ClaimStatus.ManualReview;
        return Ok(await adjudicationService.GetReviewQueueAsync(parsed, 50, ct));
    }

    /// <summary>
    /// Approves or rejects a claim with a mandatory reason code. Every decision is appended
    /// to the immutable audit trail and published to `claim.settled`.
    /// </summary>
    [HttpPost("decide")]
    public async Task<ActionResult<ClaimResponse>> Decide(
        [FromBody] AdjudicationDecisionRequest request, CancellationToken ct)
    {
        return Ok(await adjudicationService.DecideAsync(request, User.GetUserId(), ct));
    }

    /// <summary>Full claim detail (side-by-side AI extraction vs policy limits).</summary>
    [HttpGet("claims/{id:guid}")]
    public async Task<ActionResult<ClaimResponse>> ClaimDetail(Guid id, CancellationToken ct)
    {
        var claim = await claims.GetByIdWithDetailsAsync(id, ct);
        if (claim?.Policy is null) return NotFound();
        return Ok(ClaimService.MapToResponse(claim, claim.Policy));
    }
}
