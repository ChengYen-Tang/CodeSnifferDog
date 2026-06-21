using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Shared.Reports;
using System.IO.Compression;
using System.Text;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class ProjectReportExportServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void CreateMarkdown_UsesOriginalContentTypeFileNameAndUtf8Bytes()
    {
        ProjectReportExportService service = new();

        ProjectReportExportFile file = service.CreateMarkdown(new ProjectReportContentDto
        {
            ReportId = Guid.NewGuid(),
            RuleName = "Rule A",
            MarkdownContent = "# Rule A\n\nContent",
        });

        Assert.AreEqual("text/markdown; charset=utf-8", file.ContentType);
        Assert.AreEqual("Rule A.md", file.FileName);
        Assert.AreEqual("# Rule A\n\nContent", Encoding.UTF8.GetString(file.Bytes));
    }

    [TestMethod]
    public async Task CreateBundleZipAsync_UsesOriginalZipEntriesAndFileName()
    {
        ProjectReportExportService service = new();

        ProjectReportExportFile file = await service.CreateBundleZipAsync(
            new ProjectReportBundleDto
            {
                OriginalFileName = "repo.zip",
                Reports =
                [
                    new()
                    {
                        ReportId = Guid.NewGuid(),
                        RuleName = "Rule A",
                        MarkdownContent = "# Rule A",
                    },
                    new()
                    {
                        ReportId = Guid.NewGuid(),
                        RuleName = "Rule B",
                        MarkdownContent = "# Rule B",
                    },
                ],
            },
            TestContext.CancellationToken);

        Assert.AreEqual("application/zip", file.ContentType);
        Assert.AreEqual("repo-reports.zip", file.FileName);

        using MemoryStream stream = new(file.Bytes);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        Assert.AreEqual(2, archive.Entries.Count);
        Assert.AreEqual("Rule A.md", archive.Entries[0].FullName);
        Assert.AreEqual("Rule B.md", archive.Entries[1].FullName);
        Assert.AreEqual("# Rule A", await ReadEntryAsync(archive.Entries[0], TestContext.CancellationToken));
        Assert.AreEqual("# Rule B", await ReadEntryAsync(archive.Entries[1], TestContext.CancellationToken));
    }

    private static async Task<string> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using Stream stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
