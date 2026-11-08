using Microsoft.EntityFrameworkCore;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Domain.Entities;

namespace SmartTaskManagement.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        _db.Users.AnyAsync(x => x.Email == email, ct);

    public Task<bool> IsActiveUserAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.AnyAsync(x => x.Id == id && x.IsActive, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _db.Users.AddAsync(user, ct);
}

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await _db.RefreshTokens.AddAsync(token, ct);
}

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _db;

    public ProjectRepository(AppDbContext db) => _db = db;

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(Project project, CancellationToken ct = default) =>
        await _db.Projects.AddAsync(project, ct);

    public void Remove(Project project) => _db.Projects.Remove(project);
}

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;

    public TaskRepository(AppDbContext db) => _db = db;

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tasks
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(TaskItem task, CancellationToken ct = default) =>
        await _db.Tasks.AddAsync(task, ct);

    public void Remove(TaskItem task) => _db.Tasks.Remove(task);
}
