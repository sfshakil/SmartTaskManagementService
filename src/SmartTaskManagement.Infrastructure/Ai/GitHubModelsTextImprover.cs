using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTaskManagement.Application.Abstractions;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Infrastructure.Options;

namespace SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// Calls an OpenAI-compatible chat completions endpoint (GitHub Models, Gemini, ...) to improve a task description.
/// </summary>
public class GitHubModelsTextImprover : IAiTextImprover
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<GitHubModelsTextImprover> _logger;

    public GitHubModelsTextImprover(
        HttpClient http,
        IOptions<AiOptions> options,
        ILogger<GitHubModelsTextImprover> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiImprovementResult> ImproveDescriptionAsync(string? title, string description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            _logger.LogError("AI token is not configured");
            throw new ExternalServiceException("The AI service is not configured.");
        }

        var body = new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = PromptTemplates.SystemPrompt },
                new { role = "user", content = PromptTemplates.BuildUserMessage(title, description) }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("chat/completions", body, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI provider returned status {StatusCode}. Body: {Body}",
                    (int)response.StatusCode, Truncate(raw));
                throw new ExternalServiceException("The AI service is temporarily unavailable. Please try again later.");
            }

            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(raw);
            }
            catch (JsonException)
            {
                _logger.LogError(
                    "AI provider returned a non-JSON body. Status={StatusCode} ContentType={ContentType} Length={Length} Start={Start}",
                    (int)response.StatusCode,
                    response.Content.Headers.ContentType?.MediaType,
                    raw.Length,
                    Truncate(raw));
                throw new ExternalServiceException("The AI service returned an unexpected response.");
            }

            using (json)
            {
                var root = json.RootElement;

                var content = root.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                var model = root.TryGetProperty("model", out var modelElement)
                    ? modelElement.GetString() ?? _options.Model
                    : _options.Model;

                return new AiImprovementResult(content ?? string.Empty, model);
            }
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("AI provider request timed out");
            throw new ExternalServiceException("The AI service took too long to respond. Please try again.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException
            or Polly.ExecutionRejectedException)
        {
            _logger.LogError(ex, "AI provider call failed");
            throw new ExternalServiceException("The AI service is temporarily unavailable. Please try again later.");
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : value[..200];
}