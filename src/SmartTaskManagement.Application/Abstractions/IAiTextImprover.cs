namespace SmartTaskManagement.Application.Abstractions;

public sealed record AiImprovementResult(string Text, string Model);

public interface IAiTextImprover
{
    Task<AiImprovementResult> ImproveDescriptionAsync(string? title, string description, CancellationToken ct = default);
}
