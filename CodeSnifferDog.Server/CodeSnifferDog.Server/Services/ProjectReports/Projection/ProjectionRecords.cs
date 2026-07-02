namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal sealed record ProjectProjection(
    string OriginalFileName,
    IReadOnlyList<RuleReportProjection> Reports);

internal sealed record RuleReportProjection(
    Guid ReportId,
    string RuleName,
    string MarkdownContent);
