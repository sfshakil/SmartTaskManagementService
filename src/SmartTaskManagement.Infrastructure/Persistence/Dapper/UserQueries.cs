using Dapper;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Infrastructure.Persistence.Dapper;

public class UserQueries : IUserQueries
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>
    {
        ["name"] = "u.FullName {0}",
        ["email"] = "u.Email {0}",
        ["role"] = "u.Role {0}"
    };

    private readonly ISqlConnectionFactory _connections;

    public UserQueries(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken ct = default)
    {
        var where = "WHERE u.IsActive = 1";
        var search = SqlHelpers.Like(query.SearchTerm);
        if (search is not null)
        {
            where += " AND (u.FullName LIKE @Search ESCAPE '\\' OR u.Email LIKE @Search ESCAPE '\\')";
        }

        var orderBy = SqlHelpers.OrderBy(SortColumns, query.SortBy, query.Descending, "name", "u.Id");

        var sql = $"""
            SELECT COUNT(1) FROM Users u {where};
            SELECT u.Id, u.FullName, u.Email, u.Role FROM Users u {where} {orderBy}
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        using var connection = await _connections.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { Search = search, Skip = query.Skip, Take = query.Take },
            cancellationToken: ct));

        var total = await grid.ReadSingleAsync<int>();
        var rows = await grid.ReadAsync<UserRow>();
        var items = rows.Select(r => new UserDto(r.Id, r.FullName, r.Email, r.Role)).ToList();

        return new PagedResult<UserDto>(items, total, Math.Max(query.PageNumber, 1), query.Take);
    }

    private sealed class UserRow
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}
