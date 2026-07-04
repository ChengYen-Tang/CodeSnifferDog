using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports;

/// <summary>
/// Stores and retrieves generated project rule reports.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Replaces all stored reports for one project with a new set of rule drafts.
    /// </summary>
    /// <param name="projectId">Project identifier whose stored reports should be replaced.</param>
    /// <param name="reports">Replacement rule drafts.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<RuleDraft> reports,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the report list for one project.
    /// </summary>
    /// <param name="projectId">Project identifier whose report list should be loaded.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The report list, or <see langword="null" /> when the project has no stored reports.</returns>
    Task<ListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the full report bundle for one project.
    /// </summary>
    /// <param name="projectId">Project identifier whose report bundle should be loaded.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The report bundle, or <see langword="null" /> when the project has no stored reports.</returns>
    Task<BundleDto?> GetProjectReportBundleAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one stored report for a project.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the report.</param>
    /// <param name="reportId">Report identifier to load.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The report content, or <see langword="null" /> when the report does not exist.</returns>
    Task<ContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
