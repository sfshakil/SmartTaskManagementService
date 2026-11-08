using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.DTOs;

public record RegisterRequest(string FullName, string Email, string Password);

public record UpdateUserRoleRequest(UserRole Role);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record UserDto(Guid Id, string FullName, string Email, UserRole Role);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserDto User);
