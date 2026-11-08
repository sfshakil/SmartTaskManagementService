using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.DTOs;

public class CountItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class UpcomingTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public TaskItemStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime DueDateUtc { get; set; }
    public string? AssignedToName { get; set; }
}

public class DashboardDto
{
    public int TotalProjects { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int OverdueTasks { get; set; }
    public IReadOnlyList<CountItemDto> TasksByStatus { get; set; } = Array.Empty<CountItemDto>();
    public IReadOnlyList<CountItemDto> TasksByPriority { get; set; } = Array.Empty<CountItemDto>();
    public IReadOnlyList<UpcomingTaskDto> UpcomingDueTasks { get; set; } = Array.Empty<UpcomingTaskDto>();
}
