namespace ClaimGuard.Domain.Enums;

/// <summary>Stored as VARCHAR in the app_users.role column.</summary>
public enum UserRole
{
    Policyholder = 0,
    Partner = 1,
    Adjudicator = 2,
    Admin = 3
}
