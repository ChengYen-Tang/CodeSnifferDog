using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

internal interface IExportService
{
    ExportFile CreateMarkdown(ContentDto report);

    Task<ExportFile> CreateBundleZipAsync(
        BundleDto bundle,
        CancellationToken cancellationToken = default);
}
