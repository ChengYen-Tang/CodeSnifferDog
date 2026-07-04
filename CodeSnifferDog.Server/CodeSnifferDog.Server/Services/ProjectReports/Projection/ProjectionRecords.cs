namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

/// <summary>
/// Projection of one project's stored reports.
/// </summary>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="Reports">Projected rule reports for the project.</param>
internal sealed record ProjectProjection(
    string OriginalFileName,
    IReadOnlyList<RuleReportProjection> Reports);

/// <summary>
/// Projection of one stored rule report.
/// </summary>
/// <param name="ReportId">Report identifier.</param>
/// <param name="RuleName">Human-readable rule name.</param>
/// <param name="MarkdownContent">Rendered markdown content.</param>
internal sealed record RuleReportProjection(
    Guid ReportId,
    string RuleName,
    string MarkdownContent);
