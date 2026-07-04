using CodeSnifferDog.Server.Services.ProjectReports.Projection;

namespace CodeSnifferDog.Server.Services.ProjectReports.Queries;

/// <summary>
/// Loads persisted read models required for project reports.
/// </summary>
internal interface IQueryService
{
    /// <summary>
    /// Loads the project-level projection that contains all stored reports for a project.
    /// </summary>
    /// <param name="projectId">Project identifier to load.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The project projection, or <see langword="null" /> when the project has no stored reports.</returns>
    Task<ProjectProjection?> GetProjectReportsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one stored rule report projection.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the report.</param>
    /// <param name="reportId">Report identifier to load.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The rule report projection, or <see langword="null" /> when the report does not exist.</returns>
    Task<RuleReportProjection?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
