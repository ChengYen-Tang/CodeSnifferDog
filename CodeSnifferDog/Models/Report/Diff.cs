namespace CodeSnifferDog.Models.Report;

/// <summary>
/// Describes how a rule-report run changed the repository issue set.
/// </summary>
public sealed class Diff
{
    /// <summary>
    /// Gets issues created by the current report run.
    /// </summary>
    public required IReadOnlyList<StoredIssue> CreatedIssues { get; init; }

    /// <summary>
    /// Gets issues updated by the current report run.
    /// </summary>
    public required IReadOnlyList<StoredIssue> UpdatedIssues { get; init; }

    /// <summary>
    /// Gets issues deleted by the current report run.
    /// </summary>
    public required IReadOnlyList<StoredIssue> DeletedIssues { get; init; }
}
