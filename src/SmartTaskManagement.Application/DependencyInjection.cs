using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartTaskManagement.Application.Services;

namespace SmartTaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDescriptionImprovementService, DescriptionImprovementService>();

        return services;
    }
}
