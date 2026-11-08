using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Infrastructure.Security;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && UserId != Guid.Empty;

    public Guid UserId
    {
        get
        {
            var value = Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public UserRole Role
    {
        get
        {
            var value = Principal?.FindFirstValue("role") ?? Principal?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(value, out var role) ? role : UserRole.TeamMember;
        }
    }
}
