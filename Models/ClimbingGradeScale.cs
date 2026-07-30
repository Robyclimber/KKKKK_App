namespace RouteLab.Models;

public static class ClimbingGradeScale
{
    private static readonly IReadOnlyList<string> orderedGrades = new[]
    {
        "4a", "4a+",
        "4b", "4b+",
        "4c", "4c+",
        "5a", "5a+",
        "5b", "5b+",
        "5c", "5c+",
        "6a", "6a+",
        "6b", "6b+",
        "6c", "6c+",
        "7a", "7a+",
        "7b", "7b+",
        "7c", "7c+",
        "8a", "8a+",
        "8b", "8b+",
        "8c", "8c+"
    };

    public static IReadOnlyList<string> OrderedGrades => orderedGrades;

    public static string NormalizeOrEmpty(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return string.Empty;
        }

        var normalized = difficulty.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        return orderedGrades.FirstOrDefault(grade => string.Equals(grade, normalized, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    public static double ParseDifficultyFactor(string? difficulty)
    {
        var normalized = NormalizeOrEmpty(difficulty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0.50d;
        }

        var index = orderedGrades
            .Select((grade, position) => new { grade, position })
            .FirstOrDefault(item => string.Equals(item.grade, normalized, StringComparison.OrdinalIgnoreCase))
            ?.position ?? -1;

        if (index < 0)
        {
            return 0.50d;
        }

        var range = Math.Max(1, orderedGrades.Count - 1);
        return 0.20d + ((double)index / range);
    }
}

