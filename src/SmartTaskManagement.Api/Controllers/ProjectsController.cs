using Microsoft.AspNetCore.Mvc;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Application.Services;

namespace SmartTaskManagement.Api.Controllers;

public class ProjectsController : ApiControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects) => _projects = projects;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] ProjectQuery query, CancellationToken ct) =>
        OkResponse(await _projects.SearchAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        OkResponse(await _projects.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await _projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ApiResponse<ProjectDto>.Ok(project, "Project created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectRequest request, CancellationToken ct) =>
        OkResponse(await _projects.UpdateAsync(id, request, ct), "Project updated.");

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _projects.DeleteAsync(id, ct);
        return OkResponse("Project deleted.");
    }
}
