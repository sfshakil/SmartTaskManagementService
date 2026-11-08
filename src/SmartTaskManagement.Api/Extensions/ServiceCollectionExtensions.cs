using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartTaskManagement.Api.Filters;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Infrastructure.Options;

namespace SmartTaskManagement.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // Keep malformed-model responses in the same envelope as every other error.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());

                    return new BadRequestObjectResult(
                        ApiResponse.Fail("One or more validation errors occurred.", errors, context.HttpContext.TraceIdentifier));
                };
            });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Resolve options lazily so the validated JwtOptions (user secrets / env vars) are the single source of truth.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Authentication is required or the token is invalid.");
                    },
                    OnForbidden = context =>
                        WriteErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden, "You do not have permission to perform this action.")
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:4200"];

        services.AddCors(options =>
            options.AddPolicy(CorsPolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", "Accept")));

        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        var globalLimit = section.GetValue("GlobalPerMinute", 120);
        var authLimit = section.GetValue("AuthPerMinute", 10);
        var aiLimit = section.GetValue("AiPerMinute", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Baseline protection for every endpoint, partitioned by client address.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => FixedWindow(globalLimit)));

            // Credential endpoints: tight limit per client address to slow brute-force attempts.
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter($"auth:{ClientKey(context)}", _ => FixedWindow(authLimit)));

            // AI endpoint is costly: limit per authenticated user.
            options.AddPolicy("ai", context =>
            {
                var user = context.User.FindFirst("sub")?.Value ?? ClientKey(context);
                return RateLimitPartition.GetFixedWindowLimiter($"ai:{user}", _ => FixedWindow(aiLimit));
            });

            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await WriteErrorAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "Too many requests. Please try again later.", ct);
            };
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocs(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Smart Task Management API",
                Version = "v1",
                Description = "Projects, tasks, dashboard and AI-assisted task descriptions."
            });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml);
            }

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the access token only. Swagger adds the 'Bearer ' prefix.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public const string CorsPolicyName = "Frontend";

    private static FixedWindowRateLimiterOptions FixedWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    };

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static Task WriteErrorAsync(HttpContext context, int status, string message, CancellationToken ct = default)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var body = ApiResponse.Fail(message, null, context.TraceIdentifier);
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, ResponseJson), ct);
    }
}
