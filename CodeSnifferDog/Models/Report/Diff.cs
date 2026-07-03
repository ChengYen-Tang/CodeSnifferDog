namespace CodeSnifferDog.Models.Report;

public sealed class Diff
{
    public required IReadOnlyList<StoredIssue> CreatedIssues { get; init; }

    public required IReadOnlyList<StoredIssue> UpdatedIssues { get; init; }

    public required IReadOnlyList<StoredIssue> DeletedIssues { get; init; }
}
