using System.Text.Json;
using FluentValidation;
using SmartTaskManagement.Application.Common;

namespace SmartTaskManagement.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        IDictionary<string, string[]>? errors = null;
        string message;
        int status;

        switch (exception)
        {
            case ValidationException validation:
                status = StatusCodes.Status400BadRequest;
                message = "One or more validation errors occurred.";
                errors = validation.Errors
                    .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                break;
            case AuthenticationFailedException auth:
                status = StatusCodes.Status401Unauthorized;
                message = auth.Message;
                break;
            case ForbiddenException forbidden:
                status = StatusCodes.Status403Forbidden;
                message = forbidden.Message;
                break;
            case NotFoundException notFound:
                status = StatusCodes.Status404NotFound;
                message = notFound.Message;
                break;
            case ConflictException conflict:
                status = StatusCodes.Status409Conflict;
                message = conflict.Message;
                break;
            case ExternalServiceException external:
                status = StatusCodes.Status503ServiceUnavailable;
                message = external.Message;
                _logger.LogWarning("External service failure. TraceId={TraceId} Message={Message}", traceId, external.Message);
                break;
            case Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } }:
                status = StatusCodes.Status409Conflict;
                message = "A record with the same unique value already exists.";
                break;
            case BadHttpRequestException:
                status = StatusCodes.Status400BadRequest;
                message = "The request body is invalid.";
                break;
            default:
                status = StatusCodes.Status500InternalServerError;
                message = "An unexpected error occurred. Please try again later.";
                _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
                break;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var body = ApiResponse.Fail(message, errors, traceId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
