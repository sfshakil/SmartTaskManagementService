using Dapper;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Infrastructure.Persistence.Dapper;

public class ProjectQueries : IProjectQueries
{
    private const string SelectProjects = """
        SELECT p.Id, p.Name, p.Description, p.OwnerId, u.FullName AS OwnerName,
               (SELECT COUNT(1) FROM Tasks t WHERE t.ProjectId = p.Id) AS TotalTasks,
               (SELECT COUNT(1) FROM Tasks t WHERE t.ProjectId = p.Id AND t.Status = @Completed) AS CompletedTasks,
               p.CreatedAtUtc, p.UpdatedAtUtc
        FROM Projects p
        INNER JOIN Users u ON u.Id = p.OwnerId
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>
    {
        ["name"] = "p.Name {0}",
        ["createdat"] = "p.CreatedAtUtc {0}",
        ["owner"] = "u.FullName {0}",
        ["totaltasks"] = "TotalTasks {0}"
    };

    private readonly ISqlConnectionFactory _connections;

    public ProjectQueries(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<ProjectDto?> GetByIdAsync(Guid id, AccessScope scope, CancellationToken ct = default)
    {
        var sql = $"{SelectProjects} WHERE p.Id = @Id AND {ScopePredicate(scope)}";

        using var connection = await _connections.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<ProjectDto>(new CommandDefinition(
            sql,
            new { Id = id, scope.UserId, Completed = (int)TaskItemStatus.Completed },
            cancellationToken: ct));
    }

    public async Task<PagedResult<ProjectDto>> SearchAsync(ProjectQuery query, AccessScope scope, CancellationToken ct = default)
    {
        var where = $"WHERE {ScopePredicate(scope)}";
        var search = SqlHelpers.Like(query.SearchTerm);
        if (search is not null)
        {
            where += " AND (p.Name LIKE @Search ESCAPE '\\' OR p.Description LIKE @Search ESCAPE '\\')";
        }

        var orderBy = SqlHelpers.OrderBy(SortColumns, query.SortBy, query.Descending, "createdat", "p.Id");

        var sql = $"""
            SELECT COUNT(1) FROM Projects p {where};
            {SelectProjects} {where} {orderBy}
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        using var connection = await _connections.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                scope.UserId,
                Search = search,
                Skip = query.Skip,
                Take = query.Take,
                Completed = (int)TaskItemStatus.Completed
            },
            cancellationToken: ct));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<ProjectDto>()).ToList();

        return new PagedResult<ProjectDto>(items, total, Math.Max(query.PageNumber, 1), query.Take);
    }

    private static string ScopePredicate(AccessScope scope) => scope.Role switch
    {
        UserRole.Admin => "1 = 1",
        UserRole.ProjectManager => "p.OwnerId = @UserId",
        _ => "EXISTS (SELECT 1 FROM Tasks st WHERE st.ProjectId = p.Id AND st.AssignedToId = @UserId)"
    };
}
