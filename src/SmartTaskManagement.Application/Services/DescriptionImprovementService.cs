using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;

namespace SmartTaskManagement.Application.Services;

public interface IDescriptionImprovementService
{
    Task<ImproveDescriptionResponse> ImproveAsync(ImproveDescriptionRequest request, CancellationToken ct = default);
}

public partial class DescriptionImprovementService : IDescriptionImprovementService
{
    public const int MaxOutputLength = 2000;

    private readonly IAiTextImprover _improver;
    private readonly ILogger<DescriptionImprovementService> _logger;

    public DescriptionImprovementService(IAiTextImprover improver, ILogger<DescriptionImprovementService> logger)
    {
        _improver = improver;
        _logger = logger;
    }

    public async Task<ImproveDescriptionResponse> ImproveAsync(ImproveDescriptionRequest request, CancellationToken ct = default)
    {
        var title = Sanitize(request.Title);
        var description = Sanitize(request.Description) ?? string.Empty;

        var started = DateTime.UtcNow;
        var result = await _improver.ImproveDescriptionAsync(title, description, ct);
        var improved = CleanOutput(result.Text);

        _logger.LogInformation(
            "Description improved. InputLength={InputLength} OutputLength={OutputLength} ElapsedMs={ElapsedMs}",
            description.Length,
            improved.Length,
            (DateTime.UtcNow - started).TotalMilliseconds);

        return new ImproveDescriptionResponse(request.Description.Trim(), improved, result.Model);
    }

    private static string? Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var cleaned = ControlCharacters().Replace(input, string.Empty);
        cleaned = PromptTags().Replace(cleaned, string.Empty);
        return cleaned.Trim();
    }

    private static string CleanOutput(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        value = CodeFence().Replace(value, string.Empty).Trim();
        value = PromptTags().Replace(value, string.Empty).Trim();

        if (value.Length == 0)
        {
            throw new ExternalServiceException("The AI service returned an empty response.");
        }

        if (value.Length > MaxOutputLength)
        {
            value = value[..MaxOutputLength].TrimEnd();
        }

        return value;
    }

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]")]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(@"</?\s*task_(title|description)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex PromptTags();

    [GeneratedRegex(@"^```[a-zA-Z]*\s*|\s*```$")]
    private static partial Regex CodeFence();
}
