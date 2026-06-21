using CodeSnifferDog.Server.Services.ProjectReports.Projection;

namespace CodeSnifferDog.Server.Services.ProjectReports.Queries;

internal interface IProjectReportQueryService
{
    Task<ProjectReportProjectProjection?> GetProjectReportsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuleReportProjection?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
