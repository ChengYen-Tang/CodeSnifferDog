using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Report;

namespace CodeSnifferDog.Workflows.Report;

/// <summary>
/// Computes diffs between the last stored report snapshot and the current working report issues.
/// </summary>
/// <param name="reportIssueStore">Store that persists report snapshots and diffs.</param>
internal sealed class DiffService(IIssueStore reportIssueStore)
{
    private readonly IIssueStore _reportIssueStore = reportIssueStore;

    /// <summary>
    /// Computes the latest diff for one rule flow and stores it in the report issue store.
    /// </summary>
    /// <param name="ruleReportKey">Repository-wide report key used to load the previous snapshot.</param>
    /// <param name="ruleFlowKey">Current rule-flow key used to load current issues and store the diff.</param>
    /// <param name="cancellationToken">Cancels diff computation.</param>
    /// <returns>The computed diff.</returns>
    public async Task<Diff> ComputeAndStoreDiffAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredIssue> previousSnapshot =
            await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StoredIssue> currentIssues =
            await _reportIssueStore.ListAllAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);

        Diff diff = BuildDiff(previousSnapshot, currentIssues);
        await _reportIssueStore.SetLatestDiffAsync(ruleFlowKey, diff, cancellationToken).ConfigureAwait(false);
        return diff;
    }

    /// <summary>
    /// Builds a diff between the previous persisted snapshot and the current issue set.
    /// </summary>
    /// <param name="previousSnapshot">Previous persisted report snapshot.</param>
    /// <param name="currentIssues">Current issues produced by the report aggregator.</param>
    /// <returns>The computed diff.</returns>
    private static Diff BuildDiff(
        IReadOnlyList<StoredIssue> previousSnapshot,
        IReadOnlyList<StoredIssue> currentIssues)
    {
        Dictionary<string, StoredIssue> previousById = previousSnapshot.ToDictionary(issue => issue.RuleReportIssueId, StringComparer.Ordinal);
        Dictionary<string, StoredIssue> currentById = currentIssues.ToDictionary(issue => issue.RuleReportIssueId, StringComparer.Ordinal);

        List<StoredIssue> created = [];
        List<StoredIssue> updated = [];
        List<StoredIssue> deleted = [];

        foreach ((string id, StoredIssue currentIssue) in currentById)
        {
            if (!previousById.TryGetValue(id, out StoredIssue? previousIssue))
            {
                created.Add(currentIssue);
                continue;
            }

            if (!AreEquivalent(previousIssue, currentIssue))
                updated.Add(currentIssue);
        }

        foreach ((string id, StoredIssue previousIssue) in previousById)
            if (!currentById.ContainsKey(id))
                deleted.Add(previousIssue);

        return new Diff
        {
            CreatedIssues = created,
            UpdatedIssues = updated,
            DeletedIssues = deleted,
        };
    }

    /// <summary>
    /// Determines whether two stored issues are equivalent for report-diff purposes.
    /// </summary>
    /// <param name="left">First stored issue.</param>
    /// <param name="right">Second stored issue.</param>
    /// <returns><see langword="true" /> when both issues carry the same persisted report content.</returns>
    private static bool AreEquivalent(StoredIssue left, StoredIssue right)
        =>
        left.RuleReportIssueId == right.RuleReportIssueId &&
        left.IssueType == right.IssueType &&
        left.Severity == right.Severity &&
        left.FileOrFunction == right.FileOrFunction &&
        left.RelevantCodePatternOrExpression == right.RelevantCodePatternOrExpression &&
        left.WhyThisIsAProblem == right.WhyThisIsAProblem &&
        left.Confidence == right.Confidence &&
        left.FollowUpFiles == right.FollowUpFiles &&
        left.SuggestedFixDirection == right.SuggestedFixDirection &&
        left.ReviewStrategy == right.ReviewStrategy &&
        left.ScopeCoverage == right.ScopeCoverage &&
        left.CrossScopeAnalysis == right.CrossScopeAnalysis;
}
