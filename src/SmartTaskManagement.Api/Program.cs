using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using SmartTaskManagement.Api.Extensions;
using SmartTaskManagement.Api.Middleware;
using SmartTaskManagement.Application;
using SmartTaskManagement.Infrastructure;
using SmartTaskManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiControllers()
    .AddJwtAuthentication()
    .AddApiCors(builder.Configuration)
    .AddApiRateLimiting(builder.Configuration)
    .AddSwaggerDocs();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services
    .AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    }
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        headers["Cache-Control"] = "no-store";
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Task Management API v1");
        options.DocumentTitle = "Smart Task Management API";
    });

    app.MapGet("/", () =>
    {
        return Results.Redirect("/swagger");
    });
}

app.UseCors(ServiceCollectionExtensions.CorsPolicyName);
app.UseAuthentication();
// After authentication so the "ai" policy can partition by user id.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

try
{
    await DatabaseInitializer.InitializeAsync(
        app.Services,
        applyMigrations: app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true));

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
