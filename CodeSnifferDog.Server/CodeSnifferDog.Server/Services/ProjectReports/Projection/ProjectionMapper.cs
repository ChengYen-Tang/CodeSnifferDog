using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal sealed class ProjectionMapper : IProjectionMapper
{
    public ProjectReportBundleDto MapBundle(ProjectProjection project) => new()
    {
        OriginalFileName = project.OriginalFileName,
        Reports = project.Reports
            .Select(report => new ProjectRuleReportDto
            {
                ReportId = report.ReportId,
                RuleName = report.RuleName,
                MarkdownContent = report.MarkdownContent,
            })
            .ToList(),
    };

    public ProjectReportListDto MapList(ProjectProjection project) => new()
    {
        OriginalFileName = project.OriginalFileName,
        Reports = project.Reports
            .Select(report => new ProjectReportListItemDto
            {
                ReportId = report.ReportId,
                RuleName = report.RuleName,
            })
            .ToList(),
    };

    public ProjectReportContentDto MapContent(RuleReportProjection report) => new()
    {
        ReportId = report.ReportId,
        RuleName = report.RuleName,
        MarkdownContent = report.MarkdownContent,
    };
}
