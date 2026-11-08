using Microsoft.Extensions.Logging;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Entities;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.Services;

public interface ITaskService
{
    Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TaskDto> AssignAsync(Guid id, Guid? assignedToId, CancellationToken ct = default);
    Task<TaskDto> UpdateStatusAsync(Guid id, TaskItemStatus status, CancellationToken ct = default);
    Task<TaskDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<TaskDto>> SearchAsync(TaskQuery query, CancellationToken ct = default);
}

public class TaskService : ITaskService
{
    private readonly ITaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly ITaskQueries _queries;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository tasks,
        IProjectRepository projects,
        IUserRepository users,
        ITaskQueries queries,
        ICurrentUser currentUser,
        IUnitOfWork uow,
        ILogger<TaskService> logger)
    {
        _tasks = tasks;
        _projects = projects;
        _users = users;
        _queries = queries;
        _currentUser = currentUser;
        _uow = uow;
        _logger = logger;
    }

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        if (!CanManageProject(project, scope))
        {
            throw new ForbiddenException();
        }

        if (request.AssignedToId.HasValue)
        {
            await EnsureAssigneeAsync(request.AssignedToId.Value, ct);
        }

        var task = new TaskItem
        {
            ProjectId = project.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            DueDateUtc = NormalizeUtc(request.DueDateUtc),
            AssignedToId = request.AssignedToId,
            CreatedById = scope.UserId
        };

        await _tasks.AddAsync(task, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Task {TaskId} created in project {ProjectId}", task.Id, project.Id);
        return await GetAsync(task.Id, ct);
    }

    public async Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var task = await LoadForManagementAsync(id, scope, ct);

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Priority = request.Priority;
        task.DueDateUtc = NormalizeUtc(request.DueDateUtc);
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var task = await LoadForManagementAsync(id, scope, ct);

        _tasks.Remove(task);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Task {TaskId} deleted by {UserId}", id, scope.UserId);
    }

    public async Task<TaskDto> AssignAsync(Guid id, Guid? assignedToId, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var task = await LoadForManagementAsync(id, scope, ct);

        if (assignedToId.HasValue)
        {
            await EnsureAssigneeAsync(assignedToId.Value, ct);
        }

        task.AssignedToId = assignedToId;
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<TaskDto> UpdateStatusAsync(Guid id, TaskItemStatus status, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var task = await _tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task", id);

        var isAssignee = task.AssignedToId == scope.UserId;
        if (!CanManageProject(task.Project, scope) && !isAssignee)
        {
            throw new ForbiddenException();
        }

        task.Status = status;
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        return await _queries.GetByIdAsync(id, scope, ct) ?? throw new NotFoundException("Task", id);
    }

    public Task<PagedResult<TaskDto>> SearchAsync(TaskQuery query, CancellationToken ct = default) =>
        _queries.SearchAsync(query, _currentUser.ToScope(), ct);

    private async Task<TaskItem> LoadForManagementAsync(Guid id, AccessScope scope, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task", id);

        if (!CanManageProject(task.Project, scope))
        {
            throw new ForbiddenException();
        }

        return task;
    }

    private async Task EnsureAssigneeAsync(Guid userId, CancellationToken ct)
    {
        if (!await _users.IsActiveUserAsync(userId, ct))
        {
            throw new NotFoundException("User", userId);
        }
    }

    private static bool CanManageProject(Project project, AccessScope scope) =>
        scope.IsAdmin || (scope.IsProjectManager && project.OwnerId == scope.UserId);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value.ToUniversalTime(), DateTimeKind.Utc) : null;
}
