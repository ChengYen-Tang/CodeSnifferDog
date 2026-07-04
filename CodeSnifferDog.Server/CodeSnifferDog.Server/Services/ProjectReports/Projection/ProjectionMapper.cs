using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

/// <summary>
/// Maps persisted project-report projections into shared API DTOs.
/// </summary>
internal sealed class ProjectionMapper : IProjectionMapper
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ContentDto MapContent(RuleReportProjection report) => new()
    {
        ReportId = report.ReportId,
        RuleName = report.RuleName,
        MarkdownContent = report.MarkdownContent,
    };
}
