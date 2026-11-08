using SmartTaskManagement.Domain.Entities;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    UserRole Role { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

public sealed record RefreshTokenResult(string Token, string TokenHash, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);
    RefreshTokenResult CreateRefreshToken();
    string HashToken(string token);
}
