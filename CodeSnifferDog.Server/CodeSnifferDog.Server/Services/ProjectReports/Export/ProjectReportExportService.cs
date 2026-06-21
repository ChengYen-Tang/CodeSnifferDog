using CodeSnifferDog.Server.Shared.Reports;
using System.IO.Compression;
using System.Text;

namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

internal sealed class ProjectReportExportService : IProjectReportExportService
{
    public ProjectReportExportFile CreateMarkdown(ProjectReportContentDto report) =>
        new(
            Encoding.UTF8.GetBytes(report.MarkdownContent),
            "text/markdown; charset=utf-8",
            $"{report.RuleName}.md");

    public async Task<ProjectReportExportFile> CreateBundleZipAsync(
        ProjectReportBundleDto bundle,
        CancellationToken cancellationToken = default)
    {
        using MemoryStream archiveStream = new();
        using (ZipArchive archive = new(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ProjectRuleReportDto report in bundle.Reports)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"{report.RuleName}.md", CompressionLevel.Fastest);
                await using Stream entryStream = entry.Open();
                await using StreamWriter writer = new(entryStream, Encoding.UTF8, leaveOpen: false);
                await writer.WriteAsync(report.MarkdownContent.AsMemory(), cancellationToken);
            }
        }

        return new ProjectReportExportFile(
            archiveStream.ToArray(),
            "application/zip",
            $"{Path.GetFileNameWithoutExtension(bundle.OriginalFileName)}-reports.zip");
    }
}
