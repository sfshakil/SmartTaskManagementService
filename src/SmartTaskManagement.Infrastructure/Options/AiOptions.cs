using System.ComponentModel.DataAnnotations;

namespace SmartTaskManagement.Infrastructure.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    [Required]
    public string BaseUrl { get; set; } = "https://models.github.ai/inference/";

    public string Token { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "openai/gpt-4o-mini";

    [Range(0, 1)]
    public double Temperature { get; set; } = 0.3;

    [Range(50, 2000)]
    public int MaxTokens { get; set; } = 500;

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
