using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentFactoryPromptContractTests
{
    private readonly PromptAssetReader _promptAssetReader = new();
    private readonly PromptTemplateRenderer _promptTemplateRenderer = new();

    [TestMethod]
    public void ScanAgent_UsesRawScanPromptAsset()
    {
        AgentCreationResult result = new ScanAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            new InMemoryScanProjectStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            _promptAssetReader.ReadRequiredPrompt(ScanPromptAssetPaths.ScanAgentPrompt),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ScanVerifier_RendersRepositoryRootPlaceholder()
    {
        AgentCreationResult result = new ScanVerifierAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            new InMemoryScanProjectStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ScanPromptAssetPaths.ScanVerifierAgentPrompt,
                new Dictionary<string, string>
                {
                    ["RepositoryRootPath"] = AppContext.BaseDirectory,
                }),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ProjectPlanAgent_RendersRepositoryRootPlaceholder()
    {
        AgentCreationResult result = new ProjectPlanAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            new InMemoryProjectPlanTaskItemStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ProjectPlanPromptAssetPaths.ProjectPlanAgentPrompt,
                new Dictionary<string, string>
                {
                    ["RepositoryRootPath"] = AppContext.BaseDirectory,
                }),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ProjectVerifier_RendersRepositoryRootAndScanProjectJsonPlaceholders()
    {
        StoredScanProject scanProject = CreateScanProject();

        AgentCreationResult result = new ProjectVerifierAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            scanProject,
            new InMemoryProjectPlanTaskItemStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ProjectPlanPromptAssetPaths.ProjectVerifierAgentPrompt,
                new Dictionary<string, string>
                {
                    ["RepositoryRootPath"] = AppContext.BaseDirectory,
                    ["ScanProjectJson"] = CodeSnifferDogJson.Serialize(scanProject),
                }),
            result.SystemPrompt);
    }

    [TestMethod]
    public void RuleReviewAgent_RendersRuleReviewPlaceholders()
    {
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new RuleReviewAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            new InMemoryRuleReviewIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            RenderRuleScopePrompt(RuleReviewPromptAssetPaths.RuleReviewAgentPrompt, ruleMarkdown, taskItem),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ReviewVerifier_RendersRuleReviewPlaceholders()
    {
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new ReviewVerifierAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            new InMemoryRuleReviewIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            RenderRuleScopePrompt(RuleReviewPromptAssetPaths.ReviewVerifierAgentPrompt, ruleMarkdown, taskItem),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ReportAggregator_RendersRuleScopePlaceholders()
    {
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new ReportAggregatorAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            new InMemoryRuleReportIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            RenderRuleScopePrompt(ReportPromptAssetPaths.ReportAggregatorAgentPrompt, ruleMarkdown, taskItem),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ReportVerifier_RendersCurrentFlowIssuesJsonPlaceholder()
    {
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues = [CreateRuleReviewIssue()];
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new ReportVerifierAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            currentFlowIssues,
            new InMemoryRuleReportIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ReportPromptAssetPaths.ReportVerifierAgentPrompt,
                new Dictionary<string, string>
                {
                    ["RepositoryRootPath"] = AppContext.BaseDirectory,
                    ["RuleMarkdown"] = ruleMarkdown,
                    ["CurrentFlowIssuesJson"] = CodeSnifferDogJson.Serialize(currentFlowIssues),
                }),
            result.SystemPrompt);
    }

    private string RenderRuleScopePrompt(
        string promptAssetPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem) =>
        Render(
            promptAssetPath,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = AppContext.BaseDirectory,
                ["RuleMarkdown"] = ruleMarkdown,
                ["ScopeFilesJson"] = CodeSnifferDogJson.Serialize(taskItem.Files),
            });

    private string Render(string promptAssetPath, IReadOnlyDictionary<string, string> placeholders) =>
        _promptTemplateRenderer.Render(
            _promptAssetReader.ReadRequiredPrompt(promptAssetPath),
            placeholders);

    private static OperationalContextAgentCompactionOptions CreateCompactionOptions() =>
        new()
        {
            Reducer = new OperationalContextChatReducer(
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100_000,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                },
                new StaticOperationalContextSummaryPromptProvider("Summarize."),
                new StaticSummarizer()),
        };

    private static StoredScanProject CreateScanProject() =>
        new()
        {
            ScanProjectId = "scan-project",
            ProjectName = "Project",
            ProjectPath = "src/Project",
            ProjectType = "library",
            Reason = "review target",
        };

    private static StoredProjectPlanTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-item",
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = "src/Project/Program.cs",
                    TotalLines = 42,
                },
            ],
        };

    private static StoredRuleReviewIssue CreateRuleReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue",
            IssueType = "Bug",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "pattern",
            WhyThisIsAProblem = "problem",
            Confidence = "High",
            FollowUpFiles = "none",
            SuggestedFixDirection = "fix",
            ReviewStrategy = "strategy",
            ScopeCoverage = "coverage",
            CrossScopeAnalysis = "analysis",
        };

    private sealed class StaticSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                """
                Current objective
                Completed work
                Next steps
                """);
    }

    private sealed class NoOpChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
