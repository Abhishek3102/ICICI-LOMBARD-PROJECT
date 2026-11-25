using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;
using ClaimGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
[Route("api/claims")]
[Authorize]
public class ClaimsController(ClaimService claimService) : ControllerBase
{
    /// <summary>
    /// Submits a claim (policyholder self-service or partner cashless pre-authorization).
    /// Requires the X-Idempotency-Key header; duplicates return 409.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "POLICYHOLDER,PARTNER")]
    [ProducesResponseType(typeof(ClaimResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClaimResponse>> Submit(
        [FromBody] SubmitClaimRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var headerKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();

        // Header key wins over the body key so partners can retry safely.
        var effective = string.IsNullOrWhiteSpace(headerKey) ? request : request with { IdempotencyKey = headerKey! };

        var result = await claimService.SubmitClaimAsync(effective, userId, User.GetUserRole(), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Lists the caller's own submitted claims (live status tracking).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ClaimResponse>>> Mine(CancellationToken ct)
    {
        return Ok(await claimService.GetMyClaimsAsync(User.GetUserId(), ct));
    }

    /// <summary>Fetches one claim with AI reasoning. Policyholders can only see their own.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimResponse>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await claimService.GetClaimAsync(id, User.GetUserId(), User.GetUserRole(), ct));
    }
}
