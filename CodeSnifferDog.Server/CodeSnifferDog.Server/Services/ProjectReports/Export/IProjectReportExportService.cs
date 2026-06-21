using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

internal interface IProjectReportExportService
{
    ProjectReportExportFile CreateMarkdown(ProjectReportContentDto report);

    Task<ProjectReportExportFile> CreateBundleZipAsync(
        ProjectReportBundleDto bundle,
        CancellationToken cancellationToken = default);
}
