using ClaimGuard.Domain.Enums;

namespace ClaimGuard.Domain.Entities;

/// <summary>Maps to the app_users table.</summary>
public class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    /// <summary>Populated only for Hospitals / Garages (PARTNER role).</summary>
    public string? PartnerGstin { get; set; }

    /// <summary>PBKDF2 hash. Added by supabase/schema.sql on top of the base blueprint schema.</summary>
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Policy> Policies { get; set; } = new List<Policy>();
}
