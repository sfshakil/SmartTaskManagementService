using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Application.Services;

namespace SmartTaskManagement.Api.Controllers;

[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Registers a new account. New accounts always start as Team Member.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        OkResponse(await _auth.RegisterAsync(request, ClientIp, ct), "Registration successful.");

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        OkResponse(await _auth.LoginAsync(request, ClientIp, ct), "Login successful.");

    /// <summary>Exchanges a valid refresh token for a new access token and a rotated refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        OkResponse(await _auth.RefreshAsync(request.RefreshToken, ClientIp, ct));

    /// <summary>Revokes the supplied refresh token. The refresh token is the credential, so an expired access token must not block logout.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return OkResponse("Logged out.");
    }
}
