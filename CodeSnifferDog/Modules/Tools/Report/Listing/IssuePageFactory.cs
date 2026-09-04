using CodeSnifferDog.Models.Report.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Listing;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report.Listing;

/// <summary>
/// Creates bounded repository-level rule report issue pages from stored issues.
/// </summary>
internal static class IssuePageFactory
{
    private const int IssueTypePreviewLength = 120;
    private const int LocationPreviewLength = 160;

    /// <summary>
    /// Creates one issue page from a page-sized store result that may contain one look-ahead item.
    /// </summary>
    public static IssuePage Create(IReadOnlyList<ReportStoredIssue> storedIssues, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(storedIssues);

        bool hasMore = storedIssues.Count > pageSize;
        int itemCount = Math.Min(storedIssues.Count, pageSize);
        IssueListItem[] items = new IssueListItem[itemCount];

        for (int index = 0; index < itemCount; index++)
            items[index] = CreateItem(storedIssues[index]);

        return new IssuePage
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore
                ? storedIssues[itemCount - 1].RuleReportIssueId
                : null,
        };
    }

    /// <summary>
    /// Creates the compact index representation of one stored issue.
    /// </summary>
    private static IssueListItem CreateItem(ReportStoredIssue issue) =>
        new()
        {
            RuleReportIssueId = issue.RuleReportIssueId,
            Severity = issue.Severity,
            IssueTypePreview = TextPreview.Create(issue.IssueType, IssueTypePreviewLength),
            LocationPreview = TextPreview.Create(issue.FileOrFunction, LocationPreviewLength),
        };
}
