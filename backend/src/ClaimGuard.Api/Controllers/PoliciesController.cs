using ClaimGuard.Api.Extensions;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
[Route("api/policies")]
[Authorize]
public class PoliciesController(IPolicyRepository policies) : ControllerBase
{
    /// <summary>The policyholder's own policies for the self-service portal.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "POLICYHOLDER")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var list = await policies.GetByHolderAsync(User.GetUserId(), ct);
        return Ok(list.Select(ToDto));
    }

    /// <summary>Partner desk lookup by policy number for cashless pre-authorization.</summary>
    [HttpGet("lookup")]
    [Authorize(Roles = "PARTNER,ADJUDICATOR,ADMIN")]
    public async Task<IActionResult> Lookup([FromQuery] string number, CancellationToken ct)
    {
        var policy = await policies.GetByNumberAsync(number, ct);
        if (policy is null) return NotFound(new { detail = $"Policy {number} was not found." });
        return Ok(ToDto(policy));
    }

    private static object ToDto(Policy p) => new
    {
        p.Id,
        p.PolicyNumber,
        PolicyType = p.PolicyType.ToString().ToUpperInvariant(),
        p.SumInsured,
        p.RemainingBalance,
        Status = p.Status.ToString().ToUpperInvariant(),
        p.ValidFrom,
        p.ValidTo
    };
}
