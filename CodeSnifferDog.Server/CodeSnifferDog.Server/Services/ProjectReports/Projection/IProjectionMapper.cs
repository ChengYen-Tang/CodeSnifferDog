using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Projection;

/// <summary>
/// Maps persisted project-report projections into shared DTOs.
/// </summary>
internal interface IProjectionMapper
{
    /// <summary>
    /// Maps a project report projection to the shared bundle DTO.
    /// </summary>
    /// <param name="project">Persisted project projection.</param>
    /// <returns>The mapped bundle DTO.</returns>
    BundleDto MapBundle(ProjectProjection project);

    /// <summary>
    /// Maps a project report projection to the shared list DTO.
    /// </summary>
    /// <param name="project">Persisted project projection.</param>
    /// <returns>The mapped list DTO.</returns>
    ListDto MapList(ProjectProjection project);

    /// <summary>
    /// Maps a rule-report projection to the shared content DTO.
    /// </summary>
    /// <param name="report">Persisted rule-report projection.</param>
    /// <returns>The mapped content DTO.</returns>
    ContentDto MapContent(RuleReportProjection report);
}
