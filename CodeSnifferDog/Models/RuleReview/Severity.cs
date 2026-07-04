namespace CodeSnifferDog.Models.RuleReview;

/// <summary>
/// Provides normalized severity labels and ordering for rule-review findings.
/// </summary>
public static class Severity
{
    /// <summary>
    /// Highest severity label.
    /// </summary>
    public const string High = "High";

    /// <summary>
    /// Medium severity label.
    /// </summary>
    public const string Medium = "Medium";

    /// <summary>
    /// Lowest severity label.
    /// </summary>
    public const string Low = "Low";

    /// <summary>
    /// Normalizes a severity label to the project's canonical casing.
    /// </summary>
    /// <param name="severity">Severity label to normalize.</param>
    /// <returns>The normalized severity label.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="severity"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="severity"/> is not a supported severity.</exception>
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

    /// <summary>
    /// Gets the sort order used when grouping or displaying severities.
    /// </summary>
    /// <param name="severity">Severity label to sort.</param>
    /// <returns>A zero-based sort order where more severe findings sort first.</returns>
    /// <exception cref="ArgumentException">Propagated when <paramref name="severity"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Propagated when <paramref name="severity"/> is not a supported severity.</exception>
    public static int GetSortOrder(string severity) =>
        Normalize(severity) switch
        {
            High => 0,
            Medium => 1,
            Low => 2,
            _ => throw new InvalidOperationException("Unsupported severity."),
        };
}
