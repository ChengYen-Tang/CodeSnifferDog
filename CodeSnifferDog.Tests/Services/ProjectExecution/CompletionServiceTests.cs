using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class CompletionServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CompleteAnalysisAsync_NoFindingsAndDegradedFlow_ThrowsAndClearsReports()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: true);
        CompletionService service = CreateService(services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.CompleteAnalysisAsync(
                projectId,
                CreateRules(),
                new ReviewAgentTeamAnalysisResult
                {
                    PreparationSucceeded = true,
                    ReviewStageSucceeded = true,
                    HasAnyFindings = false,
                    AllRuleFlowsSucceeded = false,
                    ExecutionErrors = [],
                    RuleReports = CreateRuleReports(CreateRules(), withFindings: false),
                },
                TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "did not finish successfully");
        await AssertProjectReportCountAsync(services, projectId, expectedCount: 0);
    }

    [TestMethod]
    public async Task CompleteAnalysisAsync_FindingsExist_CompletesAndPersistsReports()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: false);
        CompletionService service = CreateService(services);
        IReadOnlyList<ProjectExecutionRuleDefinition> rules = CreateRules();

        await service.CompleteAnalysisAsync(
            projectId,
            rules,
            new ReviewAgentTeamAnalysisResult
            {
                PreparationSucceeded = true,
                ReviewStageSucceeded = false,
                HasAnyFindings = true,
                AllRuleFlowsSucceeded = false,
                ExecutionErrors = ["rule-b flow failed."],
                RuleReports = CreateRuleReports(rules, withFindings: true),
            },
            TestContext.CancellationToken);

        await AssertProjectReportCountAsync(services, projectId, expectedCount: 2);
    }

    [TestMethod]
    public async Task CompleteAnalysisAsync_FailedRerunAfterPreviousReports_ClearsReports()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: true);
        CompletionService service = CreateService(services);
        IReadOnlyList<ProjectExecutionRuleDefinition> rules = CreateRules();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.CompleteAnalysisAsync(
                projectId,
                rules,
                new ReviewAgentTeamAnalysisResult
                {
                    PreparationSucceeded = true,
                    ReviewStageSucceeded = true,
                    HasAnyFindings = false,
                    AllRuleFlowsSucceeded = false,
                    ExecutionErrors = [],
                    RuleReports = CreateRuleReports(rules, withFindings: false),
                },
                TestContext.CancellationToken));

        await AssertProjectReportCountAsync(services, projectId, expectedCount: 0);
    }

    [TestMethod]
    public async Task CompleteAnalysisAsync_ReportRuleKeyHasNoRuleNameMapping_Throws()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: false);
        CompletionService service = CreateService(services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.CompleteAnalysisAsync(
                projectId,
                CreateRules(),
                new ReviewAgentTeamAnalysisResult
                {
                    PreparationSucceeded = true,
                    ReviewStageSucceeded = true,
                    HasAnyFindings = true,
                    AllRuleFlowsSucceeded = true,
                    ExecutionErrors = [],
                    RuleReports =
                    [
                        new()
                        {
                            RuleKey = "missing-rule",
                            MarkdownContent = "# missing-rule-report.md",
                        },
                    ],
                },
                TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "Rule name mapping was not found for rule key 'missing-rule'.");
    }

    private static CompletionService CreateService(ServiceProvider services) =>
        new(services.GetRequiredService<IReportService>());

    private static ServiceProvider CreateServices()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddScoped<IProjectionMapper, ProjectionMapper>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IReportService, ReportService>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedProjectAsync(ServiceProvider services, Guid projectId, bool seedExistingReport)
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

        ProjectRecord project = new()
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = $"uploads/{projectId:N}.zip",
            Status = ProjectProcessingStatus.Reviewing,
            FileSizeBytes = 10,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            QueueTimestampUtc = nowUtc,
            ProcessingStartedAtUtc = nowUtc,
        };
        dbContext.Projects.Add(project);

        if (seedExistingReport)
        {
            dbContext.ProjectRuleReports.Add(new ProjectRuleReportRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RuleKey = "rule-a",
                RuleKeyHash = "HASH-A",
                RuleName = "Rule A",
                MarkdownContent = "# old-report.md",
                CreatedAtUtc = nowUtc,
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static IReadOnlyList<ProjectExecutionRuleDefinition> CreateRules() =>
    [
        new()
        {
            RuleKey = "rule-a",
            RuleName = "Rule A",
            RuleMarkdown = "- Rule A",
        },
        new()
        {
            RuleKey = "rule-b",
            RuleName = "Rule B",
            RuleMarkdown = "- Rule B",
        },
    ];

    private static IReadOnlyList<ReviewAgentTeamRuleReport> CreateRuleReports(
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        bool withFindings) =>
        [.. rules.Select(rule => new ReviewAgentTeamRuleReport
        {
            RuleKey = rule.RuleKey,
            MarkdownContent = withFindings
                ? $"# {rule.RuleKey}-report.md{Environment.NewLine}{Environment.NewLine}1 issue(s) were reported for this rule in the latest completed analysis."
                : $"# {rule.RuleKey}-report.md{Environment.NewLine}{Environment.NewLine}No issues were reported for this rule in the latest completed analysis.",
        })];

    private static async Task AssertProjectReportCountAsync(ServiceProvider services, Guid projectId, int expectedCount)
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        int reportCount = await dbContext.ProjectRuleReports.CountAsync(report => report.ProjectId == projectId);
        Assert.AreEqual(expectedCount, reportCount);
    }
}
