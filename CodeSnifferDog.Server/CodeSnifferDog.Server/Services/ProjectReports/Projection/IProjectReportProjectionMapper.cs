using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

internal interface IProjectReportProjectionMapper
{
    ProjectReportBundleDto MapBundle(ProjectReportProjectProjection project);

    ProjectReportListDto MapList(ProjectReportProjectProjection project);

    ProjectReportContentDto MapContent(ProjectRuleReportProjection report);
}
