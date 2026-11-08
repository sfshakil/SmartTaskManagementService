using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.Common;

public sealed record AccessScope(Guid UserId, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsProjectManager => Role == UserRole.ProjectManager;
    public bool IsTeamMember => Role == UserRole.TeamMember;
}
