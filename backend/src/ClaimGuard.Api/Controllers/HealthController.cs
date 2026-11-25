using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>Liveness probe target for Kubernetes.</summary>
    [HttpGet("/health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "healthy", service = "claimguard-api", time = DateTime.UtcNow });
}
