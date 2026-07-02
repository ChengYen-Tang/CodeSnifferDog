using CodeSnifferDog.Server.Services.ProjectReports.Projection;

namespace CodeSnifferDog.Server.Services.ProjectReports.Queries;

internal interface IQueryService
{
    Task<ProjectProjection?> GetProjectReportsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<RuleReportProjection?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
