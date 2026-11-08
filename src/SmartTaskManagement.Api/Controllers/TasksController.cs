using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Application.Services;

namespace SmartTaskManagement.Api.Controllers;

public class TasksController : ApiControllerBase
{
    private readonly ITaskService _tasks;
    private readonly IDescriptionImprovementService _improver;

    public TasksController(ITaskService tasks, IDescriptionImprovementService improver)
    {
        _tasks = tasks;
        _improver = improver;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] TaskQuery query, CancellationToken ct) =>
        OkResponse(await _tasks.SearchAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        OkResponse(await _tasks.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await _tasks.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, ApiResponse<TaskDto>.Ok(task, "Task created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTaskRequest request, CancellationToken ct) =>
        OkResponse(await _tasks.UpdateAsync(id, request, ct), "Task updated.");

    /// <summary>Assigns the task to a user, or unassigns it when assignedToId is null.</summary>
    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, AssignTaskRequest request, CancellationToken ct) =>
        OkResponse(await _tasks.AssignAsync(id, request.AssignedToId, ct), "Task assignment updated.");

    /// <summary>Changes the task status. Allowed for the project's manager, admins, and the assignee.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateTaskStatusRequest request, CancellationToken ct) =>
        OkResponse(await _tasks.UpdateStatusAsync(id, request.Status, ct), "Task status updated.");

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tasks.DeleteAsync(id, ct);
        return OkResponse("Task deleted.");
    }

    /// <summary>
    /// Returns an AI-improved version of a task description. Nothing is saved; the caller decides whether to apply it.
    /// </summary>
    [HttpPost("improve-description")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> ImproveDescription(ImproveDescriptionRequest request, CancellationToken ct) =>
        OkResponse(await _improver.ImproveAsync(request, ct));
}
