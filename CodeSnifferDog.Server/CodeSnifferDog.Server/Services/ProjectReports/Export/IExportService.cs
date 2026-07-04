using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

/// <summary>
/// Creates downloadable file payloads for project reports.
/// </summary>
internal interface IExportService
{
    /// <summary>
    /// Creates a markdown file payload for one report.
    /// </summary>
    /// <param name="report">Report content to export.</param>
    /// <returns>The markdown export payload.</returns>
    ExportFile CreateMarkdown(ContentDto report);

    /// <summary>
    /// Creates a zip bundle containing all reports for a project.
    /// </summary>
    /// <param name="bundle">Bundle content to export.</param>
    /// <param name="cancellationToken">Cancels zip creation.</param>
    /// <returns>The zip export payload.</returns>
    Task<ExportFile> CreateBundleZipAsync(
        BundleDto bundle,
        CancellationToken cancellationToken = default);
}
