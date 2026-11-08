using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Infrastructure.Ai;
using SmartTaskManagement.Infrastructure.Options;
using SmartTaskManagement.Infrastructure.Persistence;
using SmartTaskManagement.Infrastructure.Persistence.Dapper;
using SmartTaskManagement.Infrastructure.Persistence.Repositories;
using SmartTaskManagement.Infrastructure.Security;

namespace SmartTaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();

        SqlHelpers.RegisterTypeHandlers();
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IProjectQueries, ProjectQueries>();
        services.AddScoped<ITaskQueries, TaskQueries>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IUserQueries, UserQueries>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        AddAiClient(services);

        return services;
    }

    private static void AddAiClient(IServiceCollection services)
    {
        services.AddHttpClient<IAiTextImprover, GitHubModelsTextImprover>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;

                client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds + 10);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                if (!string.IsNullOrWhiteSpace(options.Token))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Token);
                }
            })
            .AddStandardResilienceHandler(resilience =>
            {
                const int timeoutSeconds = 30;
                var attempt = TimeSpan.FromSeconds(timeoutSeconds / 2);

                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                resilience.AttemptTimeout.Timeout = attempt;
                resilience.Retry.MaxRetryAttempts = 2;
                resilience.CircuitBreaker.SamplingDuration = attempt * 2;
            });
    }
}
