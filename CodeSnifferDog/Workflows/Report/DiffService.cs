using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Report;

namespace CodeSnifferDog.Workflows.Report;

internal sealed class DiffService(IRuleReportIssueStore reportIssueStore)
{
    private readonly IRuleReportIssueStore _reportIssueStore = reportIssueStore;

    public async Task<RuleReportDiff> ComputeAndStoreDiffAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredRuleReportIssue> previousSnapshot =
            await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StoredRuleReportIssue> currentIssues =
            await _reportIssueStore.ListAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);

        RuleReportDiff diff = BuildDiff(previousSnapshot, currentIssues);
        await _reportIssueStore.SetLatestDiffAsync(ruleFlowKey, diff, cancellationToken).ConfigureAwait(false);
        return diff;
    }

    private static RuleReportDiff BuildDiff(
        IReadOnlyList<StoredRuleReportIssue> previousSnapshot,
        IReadOnlyList<StoredRuleReportIssue> currentIssues)
    {
        Dictionary<string, StoredRuleReportIssue> previousById = previousSnapshot.ToDictionary(issue => issue.RuleReportIssueId, StringComparer.Ordinal);
        Dictionary<string, StoredRuleReportIssue> currentById = currentIssues.ToDictionary(issue => issue.RuleReportIssueId, StringComparer.Ordinal);

        List<StoredRuleReportIssue> created = [];
        List<StoredRuleReportIssue> updated = [];
        List<StoredRuleReportIssue> deleted = [];

        foreach ((string id, StoredRuleReportIssue currentIssue) in currentById)
        {
            if (!previousById.TryGetValue(id, out StoredRuleReportIssue? previousIssue))
            {
                created.Add(currentIssue);
                continue;
            }

            if (!AreEquivalent(previousIssue, currentIssue))
                updated.Add(currentIssue);
        }

        foreach ((string id, StoredRuleReportIssue previousIssue) in previousById)
            if (!currentById.ContainsKey(id))
                deleted.Add(previousIssue);

        return new RuleReportDiff
        {
            CreatedIssues = created,
            UpdatedIssues = updated,
            DeletedIssues = deleted,
        };
    }

    private static bool AreEquivalent(StoredRuleReportIssue left, StoredRuleReportIssue right)
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
