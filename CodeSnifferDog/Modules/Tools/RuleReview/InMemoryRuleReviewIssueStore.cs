using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public sealed class InMemoryRuleReviewIssueStore : IRuleReviewIssueStore
{
    private readonly List<StoredRuleReviewIssue> _issues = [];
    private readonly Lock _syncRoot = new();
    private NoIssueConclusion? _noIssueConclusion;

    public ValueTask<StoredRuleReviewIssue> AddAsync(RuleReviewIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);
        StoredRuleReviewIssue storedIssue = new()
        {
            RuleReviewIssueId = Guid.NewGuid().ToString("N"),
            IssueType = normalizedIssue.IssueType,
            FileOrFunction = normalizedIssue.FileOrFunction,
            RelevantCodePatternOrExpression = normalizedIssue.RelevantCodePatternOrExpression,
            WhyThisIsAProblem = normalizedIssue.WhyThisIsAProblem,
            Confidence = normalizedIssue.Confidence,
            FollowUpFiles = normalizedIssue.FollowUpFiles,
            SuggestedFixDirection = normalizedIssue.SuggestedFixDirection,
            ReviewStrategy = normalizedIssue.ReviewStrategy,
            ScopeCoverage = normalizedIssue.ScopeCoverage,
            CrossScopeAnalysis = normalizedIssue.CrossScopeAnalysis,
        };

        lock (_syncRoot)
        {
            _noIssueConclusion = null;
            _issues.Add(storedIssue);
        }

        return ValueTask.FromResult(storedIssue);
    }

    public ValueTask<StoredRuleReviewIssue> GetAsync(string ruleReviewIssueId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);

        lock (_syncRoot)
        {
            StoredRuleReviewIssue? issue = _issues.FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

            if (issue is null)
                throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

            return ValueTask.FromResult(issue);
        }
    }

    public ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredRuleReviewIssue>>([.. _issues]);
    }

    public ValueTask<StoredRuleReviewIssue> UpdateAsync(
        string ruleReviewIssueId,
        RuleReviewIssue issue,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);

        lock (_syncRoot)
        {
            int index = _issues.FindIndex(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

            if (index < 0)
                throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

            StoredRuleReviewIssue storedIssue = new()
            {
                RuleReviewIssueId = _issues[index].RuleReviewIssueId,
                IssueType = normalizedIssue.IssueType,
                FileOrFunction = normalizedIssue.FileOrFunction,
                RelevantCodePatternOrExpression = normalizedIssue.RelevantCodePatternOrExpression,
                WhyThisIsAProblem = normalizedIssue.WhyThisIsAProblem,
                Confidence = normalizedIssue.Confidence,
                FollowUpFiles = normalizedIssue.FollowUpFiles,
                SuggestedFixDirection = normalizedIssue.SuggestedFixDirection,
                ReviewStrategy = normalizedIssue.ReviewStrategy,
                ScopeCoverage = normalizedIssue.ScopeCoverage,
                CrossScopeAnalysis = normalizedIssue.CrossScopeAnalysis,
            };
            _issues[index] = storedIssue;
            return ValueTask.FromResult(storedIssue);
        }
    }

    public ValueTask<bool> DeleteAsync(string ruleReviewIssueId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);

        lock (_syncRoot)
        {
            StoredRuleReviewIssue? issue = _issues.FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

            if (issue is null)
                return ValueTask.FromResult(false);

            _issues.Remove(issue);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_noIssueConclusion);
    }

    public ValueTask SubmitNoIssueConclusionAsync(NoIssueConclusion conclusion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conclusion);
        NoIssueConclusion normalizedConclusion = NormalizeNoIssueConclusion(conclusion);

        lock (_syncRoot)
        {
            if (_issues.Count > 0)
                throw new InvalidOperationException("Cannot submit a no-issue conclusion while issues exist.");

            _noIssueConclusion = normalizedConclusion;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _issues.Clear();
            _noIssueConclusion = null;
        }

        return ValueTask.CompletedTask;
    }

    private static RuleReviewIssue NormalizeIssue(RuleReviewIssue issue)
    {
        ValidateIssue(issue);
        return new RuleReviewIssue
        {
            IssueType = issue.IssueType.Trim(),
            FileOrFunction = issue.FileOrFunction.Trim(),
            RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression.Trim(),
            WhyThisIsAProblem = issue.WhyThisIsAProblem.Trim(),
            Confidence = issue.Confidence.Trim(),
            FollowUpFiles = issue.FollowUpFiles.Trim(),
            SuggestedFixDirection = issue.SuggestedFixDirection.Trim(),
            ReviewStrategy = issue.ReviewStrategy.Trim(),
            ScopeCoverage = issue.ScopeCoverage.Trim(),
            CrossScopeAnalysis = issue.CrossScopeAnalysis.Trim(),
        };
    }

    private static void ValidateIssue(RuleReviewIssue issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.IssueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.FileOrFunction);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.RelevantCodePatternOrExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.WhyThisIsAProblem);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.Confidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.FollowUpFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.SuggestedFixDirection);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.ReviewStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.ScopeCoverage);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.CrossScopeAnalysis);
    }

    private static NoIssueConclusion NormalizeNoIssueConclusion(NoIssueConclusion conclusion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.ReviewStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.ScopeCoverage);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.CrossScopeAnalysis);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.WhyNoIssueWasFound);

        return new NoIssueConclusion
        {
            ReviewStrategy = conclusion.ReviewStrategy.Trim(),
            ScopeCoverage = conclusion.ScopeCoverage.Trim(),
            CrossScopeAnalysis = conclusion.CrossScopeAnalysis.Trim(),
            WhyNoIssueWasFound = conclusion.WhyNoIssueWasFound.Trim(),
        };
    }
}
