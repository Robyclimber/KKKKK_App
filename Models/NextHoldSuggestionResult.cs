namespace RuoteLab.Models;

public sealed class NextHoldSuggestionResult
{
    public int? SuggestedHoleNumber { get; init; }

    public string SuggestedDirection { get; init; } = string.Empty;

    public string PrimaryReason { get; init; } = string.Empty;

    public string SecondaryReason { get; init; } = string.Empty;

    public double CenterConfidence { get; init; }

    public string CenterConfidenceLabel { get; init; } = string.Empty;

    public IReadOnlyList<NextHoldSuggestionCandidate> Candidates { get; init; } = Array.Empty<NextHoldSuggestionCandidate>();
}
