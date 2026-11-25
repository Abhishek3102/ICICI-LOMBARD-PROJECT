using ClaimGuard.Domain.Entities;

namespace ClaimGuard.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(AppUser user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
