using SmartTaskManagement.Domain.Common;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDateUtc { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
}
