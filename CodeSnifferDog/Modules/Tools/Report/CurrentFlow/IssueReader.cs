using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Models.RuleReview.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Listing;

namespace CodeSnifferDog.Modules.Tools.Report.CurrentFlow;

/// <summary>
/// Exposes an immutable, bounded view of the verified issue set entering one report flow.
/// </summary>
internal sealed class IssueReader
{
    private const int IssueTypePreviewLength = 120;
    private const int LocationPreviewLength = 160;

    private readonly StoredIssue[] _issues;
    private readonly Dictionary<string, int> _indexByIssueId;

    /// <summary>
    /// Creates a stable issue snapshot for one report-flow invocation.
    /// </summary>
    /// <param name="issues">Verified rule-review issues entering the report flow.</param>
    public IssueReader(IReadOnlyList<StoredIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        _issues = [.. issues];
        _indexByIssueId = new Dictionary<string, int>(_issues.Length, StringComparer.Ordinal);

        for (int index = 0; index < _issues.Length; index++)
        {
            StoredIssue issue = _issues[index] ?? throw new ArgumentException(
                "Current flow issues cannot contain null entries.",
                nameof(issues));
            ArgumentException.ThrowIfNullOrWhiteSpace(issue.RuleReviewIssueId);

            if (!_indexByIssueId.TryAdd(issue.RuleReviewIssueId, index))
            {
                throw new ArgumentException(
                    $"Current flow issues contain duplicate id '{issue.RuleReviewIssueId}'.",
                    nameof(issues));
            }
        }
    }

    /// <summary>
    /// Gets one complete current-flow issue by its stable identifier.
    /// </summary>
    /// <param name="args">Lookup arguments supplied by the agent tool.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching immutable issue snapshot entry.</returns>
    public ValueTask<StoredIssue> GetAsync(
        GetRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        cancellationToken.ThrowIfCancellationRequested();

        string issueId = args.RuleReviewIssueId.Trim();
        return _indexByIssueId.TryGetValue(issueId, out int index)
            ? ValueTask.FromResult(_issues[index])
            : throw new KeyNotFoundException($"Current flow issue was not found: {issueId}");
    }

    /// <summary>
    /// Lists a bounded page of compact current-flow issue indexes.
    /// </summary>
    /// <param name="args">Pagination arguments supplied by the agent tool.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A compact index page and an opaque continuation cursor when more items exist.</returns>
    public ValueTask<IssuePage> ListAsync(
        ListIssuesArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        cancellationToken.ThrowIfCancellationRequested();

        int pageSize = args.PageSize ?? IssuePage.DefaultPageSize;
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, IssuePage.MaxPageSize);

        int startIndex = GetStartIndex(args.Cursor);
        int remainingCount = _issues.Length - startIndex;
        int itemCount = Math.Min(pageSize, remainingCount);
        IssueListItem[] items = new IssueListItem[itemCount];

        for (int index = 0; index < itemCount; index++)
            items[index] = CreateListItem(_issues[startIndex + index]);

        bool hasMore = remainingCount > itemCount;
        return ValueTask.FromResult(new IssuePage
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore
                ? _issues[startIndex + itemCount - 1].RuleReviewIssueId
                : null,
        });
    }

    /// <summary>
    /// Resolves the next page start in constant time from a prior result cursor.
    /// </summary>
    /// <param name="cursor">The cursor returned by a preceding page.</param>
    /// <returns>The first index to include in the requested page.</returns>
    private int GetStartIndex(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return 0;

        string issueId = cursor.Trim();
        return _indexByIssueId.TryGetValue(issueId, out int previousIndex)
            ? previousIndex + 1
            : throw new ArgumentException("The current-flow issue cursor is invalid.", nameof(cursor));
    }

    /// <summary>
    /// Creates the compact index representation of one complete issue.
    /// </summary>
    /// <param name="issue">Complete current-flow issue.</param>
    /// <returns>The bounded index item returned to the agent.</returns>
    private static IssueListItem CreateListItem(StoredIssue issue) =>
        new()
        {
            RuleReviewIssueId = issue.RuleReviewIssueId,
            Severity = issue.Severity,
            IssueTypePreview = TextPreview.Create(issue.IssueType, IssueTypePreviewLength),
            LocationPreview = TextPreview.Create(issue.FileOrFunction, LocationPreviewLength),
        };
}
