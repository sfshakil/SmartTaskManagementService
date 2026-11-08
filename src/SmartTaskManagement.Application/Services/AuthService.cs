using Microsoft.Extensions.Logging;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Entities;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        ITokenService tokens,
        ICurrentUser currentUser,
        IUnitOfWork uow,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _tokens = tokens;
        _currentUser = currentUser;
        _uow = uow;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await _users.EmailExistsAsync(email, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.TeamMember
        };

        await _users.AddAsync(user, ct);
        var response = await IssueTokensAsync(user, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} registered", user.Id);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(NormalizeEmail(request.Email), ct);

        if (user is null || !user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt from {Ip}", ipAddress);
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        var response = await IssueTokensAsync(user, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} logged in", user.Id);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await _refreshTokens.GetByHashAsync(_tokens.HashToken(refreshToken), ct)
            ?? throw new AuthenticationFailedException("Invalid refresh token.");

        if (existing.IsRevoked)
        {
            // A rotated token being presented again means it may have been stolen; kill the whole chain.
            await RevokeAllAsync(existing.UserId, ct);
            await _uow.SaveChangesAsync(ct);
            _logger.LogWarning("Revoked refresh token reuse detected for user {UserId}", existing.UserId);
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        if (existing.IsExpired || !existing.User.IsActive)
        {
            throw new AuthenticationFailedException("Refresh token has expired.");
        }

        var response = await IssueTokensAsync(existing.User, ipAddress, ct, existing);
        await _uow.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _refreshTokens.GetByHashAsync(_tokens.HashToken(refreshToken), ct);

        if (existing is null || existing.UserId != _currentUser.UserId)
        {
            return;
        }

        if (existing.IsActive)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string? ipAddress, CancellationToken ct, RefreshToken? replaced = null)
    {
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken();

        if (replaced is not null)
        {
            replaced.RevokedAtUtc = DateTime.UtcNow;
            replaced.ReplacedByTokenHash = refresh.TokenHash;
        }

        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            User = user,
            TokenHash = refresh.TokenHash,
            ExpiresAtUtc = refresh.ExpiresAtUtc,
            CreatedByIp = ipAddress
        }, ct);

        return new AuthResponse(
            access.Token,
            access.ExpiresAtUtc,
            refresh.Token,
            refresh.ExpiresAtUtc,
            new UserDto(user.Id, user.FullName, user.Email, user.Role));
    }

    private async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var active = await _refreshTokens.GetActiveByUserAsync(userId, ct);
        foreach (var token in active)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
