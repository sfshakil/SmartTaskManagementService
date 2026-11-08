using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SmartTaskManagement.Infrastructure.Persistence.Dapper;

public interface ISqlConnectionFactory
{
    Task<IDbConnection> OpenAsync(CancellationToken ct = default);
}

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}

internal sealed class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value) =>
        DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.DateTime2;
        parameter.Value = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }
}

internal static class SqlHelpers
{
    public static void RegisterTypeHandlers() => SqlMapper.AddTypeHandler(new UtcDateTimeHandler());

    public static string? Like(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var escaped = term.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

        return $"%{escaped}%";
    }

    public static string OrderBy(
        IReadOnlyDictionary<string, string> allowed,
        string? sortBy,
        bool descending,
        string defaultKey,
        string tieBreaker)
    {
        var key = sortBy is not null && allowed.ContainsKey(sortBy.ToLowerInvariant())
            ? sortBy.ToLowerInvariant()
            : defaultKey;

        var direction = descending ? "DESC" : "ASC";
        return $"ORDER BY {string.Format(allowed[key], direction)}, {tieBreaker}";
    }
}
