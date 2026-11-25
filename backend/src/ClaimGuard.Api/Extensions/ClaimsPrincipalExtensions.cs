using System.Security.Claims;
using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The JWT `sub` claim (MapInboundClaims is disabled, so no URI remapping occurs).</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token is missing the subject claim.");

    public static UserRole GetUserRole(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirst(ClaimTypes.Role)?.Value, ignoreCase: true, out var role)
            ? role
            : throw new UnauthorizedAccessException("Token is missing a valid role claim.");
}
