using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;

namespace SmartTaskManagement.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private readonly IDashboardQueries _queries;
    private readonly ICurrentUser _currentUser;

    public DashboardService(IDashboardQueries queries, ICurrentUser currentUser)
    {
        _queries = queries;
        _currentUser = currentUser;
    }

    public Task<DashboardDto> GetAsync(CancellationToken ct = default) =>
        _queries.GetAsync(_currentUser.ToScope(), ct);
}
