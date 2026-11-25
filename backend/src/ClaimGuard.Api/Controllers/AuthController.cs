using ClaimGuard.Application.Common;
using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using ClaimGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Exchanges email + password for a JWT carrying the user's role claim.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user?.PasswordHash is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        logger.LogInformation("[v0] User {Email} logged in as {Role}", user.Email, user.Role);
        return Ok(new AuthResponse(tokens.GenerateToken(user), user.Id, user.Email, user.FullName,
            user.Role.ToString().ToUpperInvariant()));
    }

    /// <summary>
    /// Self-signup for POLICYHOLDER and PARTNER roles. ADJUDICATOR and ADMIN accounts are
    /// provisioned only through SQL seeds (least privilege).
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var role = request.Role.Trim().ToUpperInvariant();
        if (role is not ("POLICYHOLDER" or "PARTNER" or "ADJUDICATOR" or "ADMIN"))
            throw new ForbiddenException("Unknown role.");

        if (role is "ADJUDICATOR" or "ADMIN")
        {
            var bootstrapSecret = configuration["Seed:BootstrapSecret"];
            if (string.IsNullOrWhiteSpace(bootstrapSecret) || request.BootstrapSecret != bootstrapSecret)
                throw new ForbiddenException("Privileged roles require the bootstrap secret.");
        }

        if (role == "PARTNER" && string.IsNullOrWhiteSpace(request.PartnerGstin))
            throw new ValidationException("PARTNER accounts must provide a GSTIN.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new ValidationException("An account with this email already exists.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            Role = role == "PARTNER" ? UserRole.Partner : UserRole.Policyholder,
            PartnerGstin = role == "PARTNER" ? request.PartnerGstin!.Trim() : null,
            PasswordHash = hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        await users.AddAsync(user, ct);
        await users.SaveAsync(ct);

        logger.LogInformation("[v0] New {Role} account registered: {Email}", user.Role, user.Email);
        return Created(string.Empty,
            new AuthResponse(tokens.GenerateToken(user), user.Id, user.Email, user.FullName,
                user.Role.ToString().ToUpperInvariant()));
    }
}
