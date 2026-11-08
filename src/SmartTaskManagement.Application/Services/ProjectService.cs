using Microsoft.Extensions.Logging;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Entities;

namespace SmartTaskManagement.Application.Services;

public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ProjectDto>> SearchAsync(ProjectQuery query, CancellationToken ct = default);
}

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectQueries _queries;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IProjectRepository projects,
        IProjectQueries queries,
        ICurrentUser currentUser,
        IUnitOfWork uow,
        ILogger<ProjectService> logger)
    {
        _projects = projects;
        _queries = queries;
        _currentUser = currentUser;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        if (scope.IsTeamMember)
        {
            throw new ForbiddenException("Only administrators and project managers can create projects.");
        }

        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = scope.UserId
        };

        await _projects.AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Project {ProjectId} created by {UserId}", project.Id, scope.UserId);
        return await GetAsync(project.Id, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var project = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project", id);
        EnsureCanManage(project, scope);

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        var project = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project", id);
        EnsureCanManage(project, scope);

        _projects.Remove(project);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Project {ProjectId} deleted by {UserId}", id, scope.UserId);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        return await _queries.GetByIdAsync(id, scope, ct) ?? throw new NotFoundException("Project", id);
    }

    public Task<PagedResult<ProjectDto>> SearchAsync(ProjectQuery query, CancellationToken ct = default) =>
        _queries.SearchAsync(query, _currentUser.ToScope(), ct);

    private static void EnsureCanManage(Project project, AccessScope scope)
    {
        var allowed = scope.IsAdmin || (scope.IsProjectManager && project.OwnerId == scope.UserId);
        if (!allowed)
        {
            throw new ForbiddenException();
        }
    }
}
