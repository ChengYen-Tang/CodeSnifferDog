using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

internal interface IExportService
{
    ExportFile CreateMarkdown(ProjectReportContentDto report);

    Task<ExportFile> CreateBundleZipAsync(
        ProjectReportBundleDto bundle,
        CancellationToken cancellationToken = default);
}
