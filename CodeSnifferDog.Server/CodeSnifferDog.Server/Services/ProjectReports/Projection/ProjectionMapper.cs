using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal sealed class ProjectionMapper : IProjectionMapper
{
    public BundleDto MapBundle(ProjectProjection project) => new()
    {
        OriginalFileName = project.OriginalFileName,
        Reports = project.Reports
            .Select(report => new RuleDto
            {
                ReportId = report.ReportId,
                RuleName = report.RuleName,
                MarkdownContent = report.MarkdownContent,
            })
            .ToList(),
    };

    public ListDto MapList(ProjectProjection project) => new()
    {
        OriginalFileName = project.OriginalFileName,
        Reports = project.Reports
            .Select(report => new ListItemDto
            {
                ReportId = report.ReportId,
                RuleName = report.RuleName,
            })
            .ToList(),
    };

    public ContentDto MapContent(RuleReportProjection report) => new()
    {
        ReportId = report.ReportId,
        RuleName = report.RuleName,
        MarkdownContent = report.MarkdownContent,
    };
}
