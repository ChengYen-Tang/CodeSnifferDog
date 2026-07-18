using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using AnalysisRuleDefinition = CodeSnifferDog.Server.Services.ProjectExecution.Analysis.RuleDefinition;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class RunnerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_LoadsRules_RunsExecutor_AndCompletesAnalysis()
    {
        Guid projectId = Guid.CreateVersion7();
        TestReviewAnalysisExecutor analysisExecutor = new(CreateAnalysisResult());
        TestAnalysisCompletionService completionService = new();
        Runner runner = CreateRunner(analysisExecutor, completionService, out _, out FixedRuleMarkdownProvider ruleMarkdownProvider);

        ProjectAnalysisContext context = new()
        {
            ProjectId = projectId,
            RepositoryRootPath = TestRepositoryPaths.RootPath,
        };

        await runner.RunAsync(context, TestContext.CancellationToken);

        Assert.AreEqual(1, ruleMarkdownProvider.LoadRulesCallCount);
        Assert.AreSame(context, analysisExecutor.Context);
        CollectionAssert.AreEqual(ruleMarkdownProvider.Rules.ToArray(), analysisExecutor.Rules!.ToArray());
        Assert.AreEqual(projectId, completionService.ProjectId);
        CollectionAssert.AreEqual(ruleMarkdownProvider.Rules.ToArray(), completionService.Rules!.ToArray());
        Assert.AreSame(analysisExecutor.Result, completionService.AnalysisResult);
    }

    [TestMethod]
    public async Task RunAsync_ClearsExistingAgentStatusData_BeforeRunningExecutor()
    {
        Guid projectId = Guid.CreateVersion7();
        TestReviewAnalysisExecutor analysisExecutor = new(CreateAnalysisResult());
        TestAnalysisCompletionService completionService = new();
        Runner runner = CreateRunner(analysisExecutor, completionService, out IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory, out _);
        await SeedAgentGroupAsync(dbContextFactory, projectId);

        await runner.RunAsync(new ProjectAnalysisContext
        {
            ProjectId = projectId,
            RepositoryRootPath = TestRepositoryPaths.RootPath,
        }, TestContext.CancellationToken);

        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        int groupCount = await dbContext.ProjectAgentGroups.CountAsync(group => group.ProjectId == projectId, TestContext.CancellationToken);
        Assert.AreEqual(0, groupCount);
        Assert.IsTrue(analysisExecutor.WasCalled);
    }

    [TestMethod]
    public async Task RunAsync_ExecutorResultFailsCompletion_StillDelegatesToCompletionService()
    {
        AnalysisResult degradedResult = new()
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = true,
            HasAnyFindings = false,
            AllRuleFlowsSucceeded = false,
            ExecutionErrors = [],
            RuleReports = [],
        };
        TestReviewAnalysisExecutor analysisExecutor = new(degradedResult);
        TestAnalysisCompletionService completionService = new()
        {
            Exception = new InvalidOperationException("Analysis did not finish successfully."),
        };
        Runner runner = CreateRunner(analysisExecutor, completionService, out _, out _);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runner.RunAsync(new ProjectAnalysisContext
            {
                ProjectId = Guid.CreateVersion7(),
                RepositoryRootPath = TestRepositoryPaths.RootPath,
            }, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "did not finish successfully");
        Assert.AreSame(degradedResult, completionService.AnalysisResult);
    }

    private static Runner CreateRunner(
        IReviewAnalysisExecutor analysisExecutor,
        ICompletionService completionService,
        out IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        out FixedRuleMarkdownProvider ruleMarkdownProvider)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        DbContextOptions<CodeSnifferDogServerDbContext> dbContextOptions =
            new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
                .UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"), databaseRoot)
                .Options;
        dbContextFactory = new TestDbContextFactory(dbContextOptions);
        ruleMarkdownProvider = new FixedRuleMarkdownProvider();

        return new Runner(
            new ReadyChatClientProvider(),
            ruleMarkdownProvider,
            analysisExecutor,
            completionService,
            dbContextFactory,
            Options.Create(new Settings
            {
                ExecutionOptions = new ExecutionOptions
                {
                    MaxParallelAgents = 1,
                    ModelContextWindowTokens = 128_000,
                    AgentRunTimeoutSeconds = 30,
                    MaxConsecutiveAgentRunFailures = 3,
                },
            }),
            NullLogger<Runner>.Instance);
    }

    private static AnalysisResult CreateAnalysisResult() =>
        new()
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = true,
            HasAnyFindings = false,
            AllRuleFlowsSucceeded = true,
            ExecutionErrors = [],
            RuleReports = [],
        };

    private static async Task SeedAgentGroupAsync(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory, Guid projectId)
    {
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.ProjectAgentGroups.Add(new ProjectAgentGroupRecord
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            RuntimeKey = "group-a",
            DisplayName = "Group A",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed class TestReviewAnalysisExecutor(AnalysisResult result) : IReviewAnalysisExecutor
    {
        public AnalysisResult Result { get; } = result;

        public bool WasCalled { get; private set; }

        public ProjectAnalysisContext? Context { get; private set; }

        public IReadOnlyList<AnalysisRuleDefinition>? Rules { get; private set; }

        public Task<AnalysisResult> AnalyzeAsync(
            ProjectAnalysisContext context,
            IReadOnlyList<AnalysisRuleDefinition> rules,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Context = context;
            Rules = rules;
            return Task.FromResult(Result);
        }
    }

    private sealed class TestAnalysisCompletionService : ICompletionService
    {
        public Guid ProjectId { get; private set; }

        public IReadOnlyList<AnalysisRuleDefinition>? Rules { get; private set; }

        public AnalysisResult? AnalysisResult { get; private set; }

        public Exception? Exception { get; init; }

        public Task CompleteAnalysisAsync(
            Guid projectId,
            IReadOnlyList<AnalysisRuleDefinition> rules,
            AnalysisResult analysisResult,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            Rules = rules;
            AnalysisResult = analysisResult;

            if (Exception is not null)
                throw Exception;

            return Task.CompletedTask;
        }
    }

    private sealed class ReadyChatClientProvider : IProjectChatClientProvider
    {
        public bool IsReady => true;

        public Microsoft.Extensions.AI.IChatClient CreateChatClient() =>
            throw new InvalidOperationException("The runner should delegate execution to IReviewAnalysisExecutor.");
    }

    private sealed class FixedRuleMarkdownProvider : IReviewRuleMarkdownProvider
    {
        public int LoadRulesCallCount { get; private set; }

        public bool HasRules => true;

        public IReadOnlyList<AnalysisRuleDefinition> Rules { get; } =
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

        public Task<IReadOnlyList<AnalysisRuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default)
        {
            LoadRulesCallCount++;
            return Task.FromResult(Rules);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<CodeSnifferDogServerDbContext> options)
        : IDbContextFactory<CodeSnifferDogServerDbContext>
    {
        public CodeSnifferDogServerDbContext CreateDbContext() => new(options);
    }
}
