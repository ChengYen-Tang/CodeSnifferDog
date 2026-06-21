using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class ProjectReportServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ReplaceProjectReportsAsync_ReplacesReportsAndComputesStableHash()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        await SeedProjectAsync(dbContextFactory, projectId, seedReports: true);
        ProjectReportService service = CreateService(dbContextFactory);

        await service.ReplaceProjectReportsAsync(
            projectId,
            [
                new()
                {
                    RuleKey = "rule-b",
                    RuleName = "Rule B",
                    MarkdownContent = "# Rule B",
                },
                new()
                {
                    RuleKey = "rule-a",
                    RuleName = "Rule A",
                    MarkdownContent = "# Rule A",
                },
            ],
            TestContext.CancellationToken);

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        List<ProjectRuleReportRecord> reports = await dbContext.ProjectRuleReports
            .OrderBy(report => report.RuleName)
            .ToListAsync(TestContext.CancellationToken);
        Assert.AreEqual(2, reports.Count);
        Assert.AreEqual("rule-a", reports[0].RuleKey);
        Assert.AreEqual(ComputeStableHash("rule-a"), reports[0].RuleKeyHash);
        Assert.AreEqual("# Rule A", reports[0].MarkdownContent);
        Assert.AreEqual("rule-b", reports[1].RuleKey);
        Assert.AreEqual(ComputeStableHash("rule-b"), reports[1].RuleKeyHash);
    }

    [TestMethod]
    public async Task ReplaceProjectReportsAsync_WhenProjectMissing_ThrowsOriginalException()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        ProjectReportService service = CreateService(dbContextFactory);
        Guid projectId = Guid.NewGuid();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.ReplaceProjectReportsAsync(projectId, [], TestContext.CancellationToken));

        Assert.AreEqual($"Project was not found: {projectId}", exception.Message);
    }

    [TestMethod]
    public async Task GetProjectReportListAsync_ReturnsReportsSortedByRuleName()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        Guid reportAId = Guid.NewGuid();
        Guid reportBId = Guid.NewGuid();
        await SeedProjectAsync(dbContextFactory, projectId, seedReports: false);
        await SeedReportsAsync(dbContextFactory, projectId, reportBId, reportAId);
        ProjectReportService service = CreateService(dbContextFactory);

        var dto = await service.GetProjectReportListAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(dto);
        Assert.AreEqual("repo.zip", dto.OriginalFileName);
        Assert.AreEqual(reportAId, dto.Reports[0].ReportId);
        Assert.AreEqual("Rule A", dto.Reports[0].RuleName);
        Assert.AreEqual(reportBId, dto.Reports[1].ReportId);
        Assert.AreEqual("Rule B", dto.Reports[1].RuleName);
    }

    [TestMethod]
    public async Task GetProjectReportBundleAsync_ReturnsReportsSortedByRuleName()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        Guid reportAId = Guid.NewGuid();
        Guid reportBId = Guid.NewGuid();
        await SeedProjectAsync(dbContextFactory, projectId, seedReports: false);
        await SeedReportsAsync(dbContextFactory, projectId, reportBId, reportAId);
        ProjectReportService service = CreateService(dbContextFactory);

        var dto = await service.GetProjectReportBundleAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(dto);
        Assert.AreEqual("repo.zip", dto.OriginalFileName);
        Assert.AreEqual(reportAId, dto.Reports[0].ReportId);
        Assert.AreEqual("Rule A", dto.Reports[0].RuleName);
        Assert.AreEqual("# Rule A", dto.Reports[0].MarkdownContent);
        Assert.AreEqual(reportBId, dto.Reports[1].ReportId);
    }

    [TestMethod]
    public async Task GetProjectReportAsync_ReturnsReportContentForProject()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        Guid otherProjectId = Guid.NewGuid();
        Guid reportId = Guid.NewGuid();
        await SeedProjectAsync(dbContextFactory, projectId, seedReports: false);
        await SeedProjectAsync(dbContextFactory, otherProjectId, seedReports: false);
        await SeedReportAsync(dbContextFactory, projectId, reportId, "Rule A", "# Rule A");
        await SeedReportAsync(dbContextFactory, otherProjectId, Guid.NewGuid(), "Rule Other", "# Other");
        ProjectReportService service = CreateService(dbContextFactory);

        var dto = await service.GetProjectReportAsync(projectId, reportId, TestContext.CancellationToken);

        Assert.IsNotNull(dto);
        Assert.AreEqual(reportId, dto.ReportId);
        Assert.AreEqual("Rule A", dto.RuleName);
        Assert.AreEqual("# Rule A", dto.MarkdownContent);
    }

    private static ProjectReportService CreateService(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory) =>
        new(dbContextFactory, new ProjectReportProjectionMapper());

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private async Task SeedProjectAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        bool seedReports)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = $"uploads/{projectId:N}.zip",
            Status = ProjectProcessingStatus.Completed,
            FileSizeBytes = 10,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            QueueTimestampUtc = nowUtc,
        });

        if (seedReports)
            dbContext.ProjectRuleReports.Add(CreateReport(projectId, Guid.NewGuid(), "old-rule", "Old Rule", "# Old"));

        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }

    private async Task SeedReportsAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        Guid reportBId,
        Guid reportAId)
    {
        await SeedReportAsync(dbContextFactory, projectId, reportBId, "Rule B", "# Rule B");
        await SeedReportAsync(dbContextFactory, projectId, reportAId, "Rule A", "# Rule A");
    }

    private async Task SeedReportAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        Guid reportId,
        string ruleName,
        string markdownContent)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        dbContext.ProjectRuleReports.Add(CreateReport(projectId, reportId, ruleName.ToLowerInvariant(), ruleName, markdownContent));
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }

    private static ProjectRuleReportRecord CreateReport(
        Guid projectId,
        Guid reportId,
        string ruleKey,
        string ruleName,
        string markdownContent) => new()
        {
            Id = reportId,
            ProjectId = projectId,
            RuleKey = ruleKey,
            RuleKeyHash = ComputeStableHash(ruleKey),
            RuleName = ruleName,
            MarkdownContent = markdownContent,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    private static string ComputeStableHash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
