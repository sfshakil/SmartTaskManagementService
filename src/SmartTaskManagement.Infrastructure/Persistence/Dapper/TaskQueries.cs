using Dapper;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Infrastructure.Persistence.Dapper;

public class TaskQueries : ITaskQueries
{
    private const string FromClause = """
        FROM Tasks t
        INNER JOIN Projects p ON p.Id = t.ProjectId
        LEFT JOIN Users a ON a.Id = t.AssignedToId
        """;

    private const string SelectTasks = """
        SELECT t.Id, t.Title, t.Description, t.Status, t.Priority, t.DueDateUtc,
               t.ProjectId, p.Name AS ProjectName,
               t.AssignedToId, a.FullName AS AssignedToName,
               t.CreatedById, t.CreatedAtUtc, t.UpdatedAtUtc
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>
    {
        ["title"] = "t.Title {0}",
        ["status"] = "t.Status {0}",
        ["priority"] = "t.Priority {0}",
        ["duedate"] = "CASE WHEN t.DueDateUtc IS NULL THEN 1 ELSE 0 END, t.DueDateUtc {0}",
        ["createdat"] = "t.CreatedAtUtc {0}",
        ["project"] = "p.Name {0}"
    };

    private readonly ISqlConnectionFactory _connections;

    public TaskQueries(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<TaskDto?> GetByIdAsync(Guid id, AccessScope scope, CancellationToken ct = default)
    {
        var sql = $"{SelectTasks} {FromClause} WHERE t.Id = @Id AND {ScopePredicate(scope)}";

        using var connection = await _connections.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<TaskDto>(new CommandDefinition(
            sql,
            new { Id = id, scope.UserId },
            cancellationToken: ct));
    }

    public async Task<PagedResult<TaskDto>> SearchAsync(TaskQuery query, AccessScope scope, CancellationToken ct = default)
    {
        var conditions = new List<string> { ScopePredicate(scope) };

        var search = SqlHelpers.Like(query.SearchTerm);
        if (search is not null)
        {
            conditions.Add("(t.Title LIKE @Search ESCAPE '\\' OR t.Description LIKE @Search ESCAPE '\\')");
        }

        if (query.ProjectId.HasValue) conditions.Add("t.ProjectId = @ProjectId");
        if (query.Status.HasValue) conditions.Add("t.Status = @Status");
        if (query.Priority.HasValue) conditions.Add("t.Priority = @Priority");
        if (query.AssignedToId.HasValue) conditions.Add("t.AssignedToId = @AssignedToId");
        if (query.DueFromUtc.HasValue) conditions.Add("t.DueDateUtc >= @DueFrom");
        if (query.DueToUtc.HasValue) conditions.Add("t.DueDateUtc <= @DueTo");

        var where = "WHERE " + string.Join(" AND ", conditions);
        var orderBy = SqlHelpers.OrderBy(SortColumns, query.SortBy, query.Descending, "createdat", "t.Id");

        var sql = $"""
            SELECT COUNT(1) {FromClause} {where};
            {SelectTasks} {FromClause} {where} {orderBy}
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        using var connection = await _connections.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                scope.UserId,
                Search = search,
                query.ProjectId,
                Status = query.Status.HasValue ? (int?)query.Status.Value : null,
                Priority = query.Priority.HasValue ? (int?)query.Priority.Value : null,
                query.AssignedToId,
                DueFrom = query.DueFromUtc,
                DueTo = query.DueToUtc,
                Skip = query.Skip,
                Take = query.Take
            },
            cancellationToken: ct));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<TaskDto>()).ToList();

        return new PagedResult<TaskDto>(items, total, Math.Max(query.PageNumber, 1), query.Take);
    }

    private static string ScopePredicate(AccessScope scope) => scope.Role switch
    {
        UserRole.Admin => "1 = 1",
        UserRole.ProjectManager => "p.OwnerId = @UserId",
        _ => "t.AssignedToId = @UserId"
    };
}
