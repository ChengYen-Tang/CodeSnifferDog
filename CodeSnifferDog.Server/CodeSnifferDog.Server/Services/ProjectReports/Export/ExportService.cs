using CodeSnifferDog.Server.Shared.Reports;
using System.IO.Compression;
using System.Text;

namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

/// <summary>
/// Creates downloadable markdown and zip exports for stored reports.
/// </summary>
internal sealed class ExportService : IExportService
{
    /// <inheritdoc />
    public ExportFile CreateMarkdown(ContentDto report) =>
        new(
            Encoding.UTF8.GetBytes(report.MarkdownContent),
            "text/markdown; charset=utf-8",
            $"{report.RuleName}.md");

    /// <inheritdoc />
    public async Task<ExportFile> CreateBundleZipAsync(
        BundleDto bundle,
        CancellationToken cancellationToken = default)
    {
        using MemoryStream archiveStream = new();
        using (ZipArchive archive = new(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (RuleDto report in bundle.Reports)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"{report.RuleName}.md", CompressionLevel.Fastest);
                await using Stream entryStream = entry.Open();
                await using StreamWriter writer = new(entryStream, Encoding.UTF8, leaveOpen: false);
                await writer.WriteAsync(report.MarkdownContent.AsMemory(), cancellationToken);
            }
        }

        return new ExportFile(
            archiveStream.ToArray(),
            "application/zip",
            $"{Path.GetFileNameWithoutExtension(bundle.OriginalFileName)}-reports.zip");
    }
}
