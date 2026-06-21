using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal sealed class ProjectReportProjectionMapper : IProjectReportProjectionMapper
{
    public ProjectReportBundleDto MapBundle(ProjectReportProjectProjection project) => new()
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

    public ProjectReportListDto MapList(ProjectReportProjectProjection project) => new()
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

    public ProjectReportContentDto MapContent(ProjectRuleReportProjection report) => new()
    {
        ReportId = report.ReportId,
        RuleName = report.RuleName,
        MarkdownContent = report.MarkdownContent,
    };
}
