using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;
using SmartTaskManagement.Domain.Enums;

namespace SmartTaskManagement.Application.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken ct = default);
    Task<UserDto> UpdateRoleAsync(Guid userId, UserRole role, CancellationToken ct = default);
}

public class UserService : IUserService
{
    private readonly IUserQueries _queries;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;

    public UserService(IUserQueries queries, IUserRepository users, ICurrentUser currentUser, IUnitOfWork uow)
    {
        _queries = queries;
        _users = users;
        _currentUser = currentUser;
        _uow = uow;
    }

    public Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        if (scope.IsTeamMember)
        {
            throw new ForbiddenException();
        }

        return _queries.SearchAsync(query, ct);
    }

    public async Task<UserDto> UpdateRoleAsync(Guid userId, UserRole role, CancellationToken ct = default)
    {
        var scope = _currentUser.ToScope();
        if (!scope.IsAdmin)
        {
            throw new ForbiddenException();
        }

        if (scope.UserId == userId)
        {
            throw new ConflictException("You cannot change your own role.");
        }

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        user.Role = role;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.FullName, user.Email, user.Role);
    }
}
