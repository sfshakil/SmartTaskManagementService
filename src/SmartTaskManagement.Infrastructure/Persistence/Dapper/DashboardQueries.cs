using Dapper;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Infrastructure.Persistence.Dapper;

public class DashboardQueries : IDashboardQueries
{
    private const int UpcomingWindowDays = 7;
    private const int UpcomingLimit = 10;

    private const string TaskFrom = """
        FROM Tasks t
        INNER JOIN Projects p ON p.Id = t.ProjectId
        LEFT JOIN Users a ON a.Id = t.AssignedToId
        """;

    private readonly ISqlConnectionFactory _connections;

    public DashboardQueries(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<DashboardDto> GetAsync(AccessScope scope, CancellationToken ct = default)
    {
        var projectScope = scope.Role switch
        {
            UserRole.Admin => "1 = 1",
            UserRole.ProjectManager => "p.OwnerId = @UserId",
            _ => "EXISTS (SELECT 1 FROM Tasks st WHERE st.ProjectId = p.Id AND st.AssignedToId = @UserId)"
        };

        var taskScope = scope.Role switch
        {
            UserRole.Admin => "1 = 1",
            UserRole.ProjectManager => "p.OwnerId = @UserId",
            _ => "t.AssignedToId = @UserId"
        };

        var sql = $"""
            SELECT COUNT(1) FROM Projects p WHERE {projectScope};

            SELECT COUNT(1) AS Total,
                   ISNULL(SUM(CASE WHEN t.Status = @Completed THEN 1 ELSE 0 END), 0) AS Completed,
                   ISNULL(SUM(CASE WHEN t.Status IN (@ToDo, @InProgress) THEN 1 ELSE 0 END), 0) AS Pending,
                   ISNULL(SUM(CASE WHEN t.Status IN (@ToDo, @InProgress) AND t.DueDateUtc < @Now THEN 1 ELSE 0 END), 0) AS Overdue
            {TaskFrom} WHERE {taskScope};

            SELECT t.Status AS Code, COUNT(1) AS Total {TaskFrom} WHERE {taskScope} GROUP BY t.Status;

            SELECT t.Priority AS Code, COUNT(1) AS Total {TaskFrom} WHERE {taskScope} GROUP BY t.Priority;

            SELECT TOP (@UpcomingLimit)
                   t.Id, t.Title, t.ProjectId, p.Name AS ProjectName, t.Status, t.Priority,
                   t.DueDateUtc, a.FullName AS AssignedToName
            {TaskFrom}
            WHERE {taskScope}
              AND t.Status IN (@ToDo, @InProgress)
              AND t.DueDateUtc >= @Now AND t.DueDateUtc <= @Until
            ORDER BY t.DueDateUtc ASC, t.Id;
            """;

        var now = DateTime.UtcNow;

        using var connection = await _connections.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                scope.UserId,
                Now = now,
                Until = now.AddDays(UpcomingWindowDays),
                UpcomingLimit,
                ToDo = (int)TaskItemStatus.ToDo,
                InProgress = (int)TaskItemStatus.InProgress,
                Completed = (int)TaskItemStatus.Completed
            },
            cancellationToken: ct));

        var totalProjects = await grid.ReadSingleAsync<int>();
        var totals = await grid.ReadSingleAsync<TaskTotals>();
        var byStatus = (await grid.ReadAsync<CodeCount>()).ToDictionary(x => x.Code, x => x.Total);
        var byPriority = (await grid.ReadAsync<CodeCount>()).ToDictionary(x => x.Code, x => x.Total);
        var upcoming = (await grid.ReadAsync<UpcomingTaskDto>()).ToList();

        return new DashboardDto
        {
            TotalProjects = totalProjects,
            TotalTasks = totals.Total,
            CompletedTasks = totals.Completed,
            PendingTasks = totals.Pending,
            OverdueTasks = totals.Overdue,
            TasksByStatus = Enum.GetValues<TaskItemStatus>()
                .Select(s => new CountItemDto { Name = s.ToString(), Count = byStatus.GetValueOrDefault((int)s) })
                .ToList(),
            TasksByPriority = Enum.GetValues<TaskPriority>()
                .Select(p => new CountItemDto { Name = p.ToString(), Count = byPriority.GetValueOrDefault((int)p) })
                .ToList(),
            UpcomingDueTasks = upcoming
        };
    }

    private sealed class TaskTotals
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Pending { get; set; }
        public int Overdue { get; set; }
    }

    private sealed class CodeCount
    {
        public int Code { get; set; }
        public int Total { get; set; }
    }
}
