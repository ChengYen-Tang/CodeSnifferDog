using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal interface IProjectionMapper
{
    BundleDto MapBundle(ProjectProjection project);

    ListDto MapList(ProjectProjection project);

    ContentDto MapContent(RuleReportProjection report);
}
