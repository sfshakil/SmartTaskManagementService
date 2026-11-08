using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTaskManagement.Application.Common;

namespace SmartTaskManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    protected IActionResult OkResponse<T>(T data, string? message = null) =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult OkResponse(string? message = null) =>
        Ok(ApiResponse.Ok(message));
}
