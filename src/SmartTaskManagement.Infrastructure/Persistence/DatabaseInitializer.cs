using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Domain.Entities;
using SmartTaskManagement.Domain.Enums;
using SmartTaskManagement.Infrastructure.Options;

namespace SmartTaskManagement.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool applyMigrations, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        if (applyMigrations)
        {
            if (db.Database.GetMigrations().Any())
            {
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database migrations applied");
            }
            else
            {
                // No migrations generated yet: create the schema from the model so the app still starts.
                // Run `dotnet ef migrations add InitialCreate` to switch to migrations (see README).
                logger.LogWarning("No EF Core migrations found; creating schema from the current model");
                await db.Database.EnsureCreatedAsync(ct);
            }
        }

        await SeedAdminAsync(
            db,
            provider.GetRequiredService<IPasswordHasher>(),
            provider.GetRequiredService<IOptions<SeedOptions>>().Value,
            logger,
            ct);
    }

    private static async Task SeedAdminAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        SeedOptions seed,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            return;
        }

        var email = seed.AdminEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct))
        {
            return;
        }

        db.Users.Add(new User
        {
            FullName = seed.AdminFullName,
            Email = email,
            PasswordHash = hasher.Hash(seed.AdminPassword),
            Role = UserRole.Admin
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded initial administrator account");
    }
}
