using Microsoft.AspNetCore.Mvc;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Application.Services;

namespace SmartTaskManagement.Api.Controllers;

public class UsersController : ApiControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    /// <summary>Lists active users (used to pick task assignees). Admin and Project Manager only.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] UserQuery query, CancellationToken ct) =>
        OkResponse(await _users.SearchAsync(query, ct));

    /// <summary>Changes a user's role. Admin only.</summary>
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, UpdateUserRoleRequest request, CancellationToken ct) =>
        OkResponse(await _users.UpdateRoleAsync(id, request.Role, ct), "Role updated.");
}
