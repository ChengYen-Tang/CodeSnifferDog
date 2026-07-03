using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class ReportServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ReplaceProjectReportsAsync_ReplacesReportsAndComputesStableHash()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        await SeedProjectAsync(dbContextFactory, projectId, seedReports: true);
        ReportService service = CreateService(dbContextFactory, new StubQueryService());

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
        ReportService service = CreateService(dbContextFactory, new StubQueryService());
        Guid projectId = Guid.NewGuid();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.ReplaceProjectReportsAsync(projectId, [], TestContext.CancellationToken));

        Assert.AreEqual($"Project was not found: {projectId}", exception.Message);
    }

    [TestMethod]
    public async Task GetProjectReportListAsync_UsesQueryServiceAndMapper()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        ProjectProjection projectProjection = CreateProjectProjection();
        StubQueryService queryService = new(projectProjection);
        TrackingProjectionMapper mapper = new();
        ReportService service = CreateService(
            dbContextFactory,
            queryService,
            mapper);

        var dto = await service.GetProjectReportListAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(dto);
        Assert.AreEqual(projectId, queryService.ProjectReportsProjectId);
        Assert.AreSame(projectProjection, mapper.ListProjection);
        Assert.AreEqual(1, mapper.MapListCallCount);
        Assert.AreEqual(0, mapper.MapBundleCallCount);
        Assert.AreEqual(0, mapper.MapContentCallCount);
    }

    [TestMethod]
    public async Task GetProjectReportBundleAsync_UsesQueryServiceAndMapper()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        ProjectProjection projectProjection = CreateProjectProjection();
        StubQueryService queryService = new(projectProjection);
        TrackingProjectionMapper mapper = new();
        ReportService service = CreateService(
            dbContextFactory,
            queryService,
            mapper);

        var dto = await service.GetProjectReportBundleAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(dto);
        Assert.AreEqual(projectId, queryService.ProjectReportsProjectId);
        Assert.AreSame(projectProjection, mapper.BundleProjection);
        Assert.AreEqual(1, mapper.MapBundleCallCount);
        Assert.AreEqual(0, mapper.MapListCallCount);
        Assert.AreEqual(0, mapper.MapContentCallCount);
    }

    [TestMethod]
    public async Task GetProjectReportAsync_UsesQueryServiceAndMapper()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        Guid reportId = Guid.NewGuid();
        RuleReportProjection reportProjection = new(reportId, "Rule A", "# Rule A");
        StubQueryService queryService = new(reportProjection: reportProjection);
        TrackingProjectionMapper mapper = new();
        ReportService service = CreateService(
            dbContextFactory,
            queryService,
            mapper);

        var dto = await service.GetProjectReportAsync(projectId, reportId, TestContext.CancellationToken);

        Assert.IsNotNull(dto);
        Assert.AreEqual(projectId, queryService.ProjectReportProjectId);
        Assert.AreEqual(reportId, queryService.ProjectReportReportId);
        Assert.AreSame(reportProjection, mapper.ContentProjection);
        Assert.AreEqual(1, mapper.MapContentCallCount);
        Assert.AreEqual(0, mapper.MapListCallCount);
        Assert.AreEqual(0, mapper.MapBundleCallCount);
    }

    private static ReportService CreateService(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        IQueryService queryService,
        IProjectionMapper? projectionMapper = null) =>
        new(dbContextFactory, queryService, projectionMapper ?? new ProjectionMapper());

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

    private static ProjectProjection CreateProjectProjection() =>
        new("repo.zip", [new RuleReportProjection(Guid.NewGuid(), "Rule A", "# Rule A")]);

    private sealed class StubQueryService(
        ProjectProjection? projectProjection = null,
        RuleReportProjection? reportProjection = null) : IQueryService
    {
        public Guid? ProjectReportsProjectId { get; private set; }

        public Guid? ProjectReportProjectId { get; private set; }

        public Guid? ProjectReportReportId { get; private set; }

        public Task<ProjectProjection?> GetProjectReportsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            ProjectReportsProjectId = projectId;
            return Task.FromResult(projectProjection);
        }

        public Task<RuleReportProjection?> GetProjectReportAsync(
            Guid projectId,
            Guid reportId,
            CancellationToken cancellationToken = default)
        {
            ProjectReportProjectId = projectId;
            ProjectReportReportId = reportId;
            return Task.FromResult(reportProjection);
        }
    }

    private sealed class TrackingProjectionMapper : IProjectionMapper
    {
        private readonly ProjectionMapper _inner = new();

        public int MapBundleCallCount { get; private set; }

        public int MapListCallCount { get; private set; }

        public int MapContentCallCount { get; private set; }

        public ProjectProjection? BundleProjection { get; private set; }

        public ProjectProjection? ListProjection { get; private set; }

        public RuleReportProjection? ContentProjection { get; private set; }

        public BundleDto MapBundle(ProjectProjection project)
        {
            MapBundleCallCount++;
            BundleProjection = project;
            return _inner.MapBundle(project);
        }

        public ListDto MapList(ProjectProjection project)
        {
            MapListCallCount++;
            ListProjection = project;
            return _inner.MapList(project);
        }

        public ContentDto MapContent(RuleReportProjection report)
        {
            MapContentCallCount++;
            ContentProjection = report;
            return _inner.MapContent(report);
        }
    }
}
