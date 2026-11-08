using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;

namespace SmartTaskManagement.Application.Abstractions;

public interface IProjectQueries
{
    Task<ProjectDto?> GetByIdAsync(Guid id, AccessScope scope, CancellationToken ct = default);
    Task<PagedResult<ProjectDto>> SearchAsync(ProjectQuery query, AccessScope scope, CancellationToken ct = default);
}

public interface ITaskQueries
{
    Task<TaskDto?> GetByIdAsync(Guid id, AccessScope scope, CancellationToken ct = default);
    Task<PagedResult<TaskDto>> SearchAsync(TaskQuery query, AccessScope scope, CancellationToken ct = default);
}

public interface IDashboardQueries
{
    Task<DashboardDto> GetAsync(AccessScope scope, CancellationToken ct = default);
}

public interface IUserQueries
{
    Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken ct = default);
}
