namespace CodeSnifferDog.Models.RuleReview;

public static class RuleReviewSeverity
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static string Normalize(string severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);

        return severity.Trim().ToUpperInvariant() switch
        {
            "HIGH" => High,
            "MEDIUM" => Medium,
            "LOW" => Low,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), "Severity must be High, Medium, or Low."),
        };
    }

    public static int GetSortOrder(string severity) =>
        Normalize(severity) switch
        {
            High => 0,
            Medium => 1,
            Low => 2,
            _ => throw new InvalidOperationException("Unsupported severity."),
        };
}
