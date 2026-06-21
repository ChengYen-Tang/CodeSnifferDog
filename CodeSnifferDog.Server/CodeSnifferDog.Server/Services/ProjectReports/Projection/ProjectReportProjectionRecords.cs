namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal sealed record ProjectReportProjectProjection(
    string OriginalFileName,
    IReadOnlyList<ProjectRuleReportProjection> Reports);

internal sealed record ProjectRuleReportProjection(
    Guid ReportId,
    string RuleName,
    string MarkdownContent);
