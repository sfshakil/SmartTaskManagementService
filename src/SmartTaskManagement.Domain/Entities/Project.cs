using SmartTaskManagement.Domain.Common;

namespace SmartTaskManagement.Domain.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
