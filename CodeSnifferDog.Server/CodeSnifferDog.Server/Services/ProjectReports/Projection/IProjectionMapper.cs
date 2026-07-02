using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal interface IProjectionMapper
{
    ProjectReportBundleDto MapBundle(ProjectProjection project);

    ProjectReportListDto MapList(ProjectProjection project);

    ProjectReportContentDto MapContent(RuleReportProjection report);
}
