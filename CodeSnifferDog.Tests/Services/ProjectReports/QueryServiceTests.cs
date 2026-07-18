using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class QueryServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetProjectReportsAsync_ReturnsProjectAndReportsSortedByRuleName()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.CreateVersion7();
        Guid reportAId = Guid.CreateVersion7();
        Guid reportBId = Guid.CreateVersion7();
        await SeedProjectAsync(dbContextFactory, projectId);
        await SeedReportAsync(dbContextFactory, projectId, reportBId, "rule-b", "rule b", "# Rule B");
        await SeedReportAsync(dbContextFactory, projectId, reportAId, "rule-a", "Rule A", "# Rule A");
        QueryService service = new(dbContextFactory);

        ProjectProjection? projection = await service.GetProjectReportsAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(projection);
        Assert.AreEqual("repo.zip", projection.OriginalFileName);
        Assert.AreEqual(reportAId, projection.Reports[0].ReportId);
        Assert.AreEqual("Rule A", projection.Reports[0].RuleName);
        Assert.AreEqual("# Rule A", projection.Reports[0].MarkdownContent);
        Assert.AreEqual(reportBId, projection.Reports[1].ReportId);
        Assert.AreEqual("rule b", projection.Reports[1].RuleName);
    }

    [TestMethod]
    public async Task GetProjectReportsAsync_WhenProjectHasNoReports_ReturnsEmptyReports()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.CreateVersion7();
        await SeedProjectAsync(dbContextFactory, projectId);
        QueryService service = new(dbContextFactory);

        ProjectProjection? projection = await service.GetProjectReportsAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(projection);
        Assert.AreEqual("repo.zip", projection.OriginalFileName);
        Assert.AreEqual(0, projection.Reports.Count);
    }

    [TestMethod]
    public async Task GetProjectReportsAsync_WhenProjectIsMissing_ReturnsNull()
    {
        QueryService service = new(CreateDbContextFactory());

        ProjectProjection? projection = await service.GetProjectReportsAsync(Guid.CreateVersion7(), TestContext.CancellationToken);

        Assert.IsNull(projection);
    }

    [TestMethod]
    public async Task GetProjectReportAsync_MatchesProjectIdAndReportId()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.CreateVersion7();
        Guid otherProjectId = Guid.CreateVersion7();
        Guid reportId = Guid.CreateVersion7();
        await SeedProjectAsync(dbContextFactory, projectId);
        await SeedProjectAsync(dbContextFactory, otherProjectId);
        await SeedReportAsync(dbContextFactory, projectId, reportId, "rule-a", "Rule A", "# Rule A");
        await SeedReportAsync(dbContextFactory, otherProjectId, Guid.CreateVersion7(), "rule-other", "Rule Other", "# Other");
        QueryService service = new(dbContextFactory);

        RuleReportProjection? report = await service.GetProjectReportAsync(
            projectId,
            reportId,
            TestContext.CancellationToken);

        Assert.IsNotNull(report);
        Assert.AreEqual(reportId, report.ReportId);
        Assert.AreEqual("Rule A", report.RuleName);
        Assert.AreEqual("# Rule A", report.MarkdownContent);

        RuleReportProjection? missing = await service.GetProjectReportAsync(
            otherProjectId,
            reportId,
            TestContext.CancellationToken);
        Assert.IsNull(missing);
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private async Task SeedProjectAsync(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory, Guid projectId)
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
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }

    private async Task SeedReportAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        Guid reportId,
        string ruleKey,
        string ruleName,
        string markdownContent)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        dbContext.ProjectRuleReports.Add(new ProjectRuleReportRecord
        {
            Id = reportId,
            ProjectId = projectId,
            RuleKey = ruleKey,
            RuleKeyHash = ruleKey,
            RuleName = ruleName,
            MarkdownContent = markdownContent,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }
}
