using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.DTOs;

public record CreateTaskRequest(
    Guid ProjectId,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime? DueDateUtc,
    Guid? AssignedToId);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime? DueDateUtc);

public record UpdateTaskStatusRequest(TaskItemStatus Status);

public record AssignTaskRequest(Guid? AssignedToId);

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class TaskQuery : PagedQuery
{
    public Guid? ProjectId { get; set; }
    public TaskItemStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public Guid? AssignedToId { get; set; }
    public DateTime? DueFromUtc { get; set; }
    public DateTime? DueToUtc { get; set; }
}
