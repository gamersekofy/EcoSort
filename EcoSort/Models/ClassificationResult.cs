namespace EcoSort.Models;

public sealed class ClassificationResult
{
    public required string Category { get; init; }

    public required string DisplayName { get; init; }

    public required float Confidence { get; init; }

    public required string ConfidenceLevel { get; init; }

    public required string Explanation { get; init; }

    public required string DisposalGuidance { get; init; }

    public bool IsLowConfidence => ConfidenceLevel == "Low";
}
