using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectExecution;
using CodeSnifferDog.Server.Services.ProjectReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectAnalysisRunnerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_NoFindingsAndDegradedFlow_ThrowsAndClearsReports()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: true);
        ProjectAnalysisRunner runner = CreateRunner(
            services,
            (_, rules, _) => Task.FromResult(new ReviewAgentTeamAnalysisResult
            {
                PreparationSucceeded = true,
                ReviewStageSucceeded = true,
                HasAnyFindings = false,
                AllRuleFlowsSucceeded = false,
                ExecutionErrors = [],
                RuleReports = CreateRuleReports(rules, withFindings: false),
            }));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runner.RunAsync(new ProjectAnalysisContext
            {
                ProjectId = projectId,
                RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog",
            }, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "did not finish successfully");
        await AssertProjectReportCountAsync(services, projectId, expectedCount: 0);
    }

    [TestMethod]
    public async Task RunAsync_FindingsExist_CompletesAndPersistsReports()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: false);
        ProjectAnalysisRunner runner = CreateRunner(
            services,
            (_, rules, _) => Task.FromResult(new ReviewAgentTeamAnalysisResult
            {
                PreparationSucceeded = true,
                ReviewStageSucceeded = false,
                HasAnyFindings = true,
                AllRuleFlowsSucceeded = false,
                ExecutionErrors = ["rule-b flow failed."],
                RuleReports = CreateRuleReports(rules, withFindings: true),
            }));

        await runner.RunAsync(new ProjectAnalysisContext
        {
            ProjectId = projectId,
            RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog",
        }, TestContext.CancellationToken);

        await AssertProjectReportCountAsync(services, projectId, expectedCount: 2);
    }

    [TestMethod]
    public async Task RunAsync_FailedRerunAfterPreviousReports_ClearsReports()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        await SeedProjectAsync(services, projectId, seedExistingReport: true);
        ProjectAnalysisRunner runner = CreateRunner(
            services,
            (_, rules, _) => Task.FromResult(new ReviewAgentTeamAnalysisResult
            {
                PreparationSucceeded = true,
                ReviewStageSucceeded = true,
                HasAnyFindings = false,
                AllRuleFlowsSucceeded = false,
                ExecutionErrors = [],
                RuleReports = CreateRuleReports(rules, withFindings: false),
            }));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runner.RunAsync(new ProjectAnalysisContext
            {
                ProjectId = projectId,
                RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog",
            }, TestContext.CancellationToken));

        await AssertProjectReportCountAsync(services, projectId, expectedCount: 0);
    }

    private static ProjectAnalysisRunner CreateRunner(
        ServiceProvider services,
        Func<ProjectAnalysisContext, IReadOnlyList<ProjectExecutionRuleDefinition>, CancellationToken, Task<ReviewAgentTeamAnalysisResult>> analysisOverride) =>
        new(
            new ReadyChatClientProvider(),
            new FixedRuleMarkdownProvider(),
            services.GetRequiredService<IProjectReportService>(),
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
            services.GetRequiredService<IProjectAgentStatusLiveUpdateNotifier>(),
            Options.Create(new ProjectExecutionOptions
            {
                ExecutionOptions = new ExecutionOptions
                {
                    MaxParallelAgents = 1,
                    ModelContextWindowTokens = 128_000,
                    AgentRunTimeoutSeconds = 30,
                    MaxConsecutiveAgentRunFailures = 3,
                },
            }),
            NullLoggerFactory.Instance,
            services,
            NullLogger<ProjectAnalysisRunner>.Instance,
            analysisOverride);

    private static ServiceProvider CreateServices()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddScoped<IProjectReportService, ProjectReportService>();
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier, NoOpProjectAgentStatusLiveUpdateNotifier>();
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

    private sealed class ReadyChatClientProvider : IProjectChatClientProvider
    {
        public bool IsReady => true;

        public Microsoft.Extensions.AI.IChatClient CreateChatClient() =>
            throw new InvalidOperationException("The analysis override should bypass chat client creation.");
    }

    private sealed class FixedRuleMarkdownProvider : IReviewRuleMarkdownProvider
    {
        public bool HasRules => true;

        public Task<IReadOnlyList<ProjectExecutionRuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectExecutionRuleDefinition>>(
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
            ]);
    }

    private sealed class NoOpProjectAgentStatusLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public Task NotifyAsync(CodeSnifferDog.Server.Shared.AgentStatus.ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
