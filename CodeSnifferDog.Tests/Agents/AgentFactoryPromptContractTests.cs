using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
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
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ProjectPlanAgentFactory = CodeSnifferDog.Agents.ProjectPlan.AgentFactory;
using ProjectVerifierFactory = CodeSnifferDog.Agents.ProjectPlan.VerifierFactory;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.InMemoryIssueStore;
using RuleReviewAgentFactory = CodeSnifferDog.Agents.RuleReview.AgentFactory;
using RuleReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.InMemoryIssueStore;
using ReviewVerifierFactory = CodeSnifferDog.Agents.RuleReview.VerifierFactory;

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
            _promptAssetReader.ReadRequiredPrompt(ScanAgentPromptAssets.ScanAgentPrompt),
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
                ScanAgentPromptAssets.ScanVerifierAgentPrompt,
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
            new InMemoryTaskItemStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ProjectPlanAgentPromptAssets.ProjectPlanAgentPrompt,
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

        AgentCreationResult result = new ProjectVerifierFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            scanProject,
            new InMemoryTaskItemStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ProjectPlanAgentPromptAssets.ProjectVerifierAgentPrompt,
                new Dictionary<string, string>
                {
                    ["RepositoryRootPath"] = AppContext.BaseDirectory,
                    ["ScanProjectJson"] = CodeSnifferDogJson.Serialize(scanProject),
                }),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ProjectPlanPrompts_UseConsistentLargeSingleFilePolicy()
    {
        string plannerPrompt = _promptAssetReader.ReadRequiredPrompt(ProjectPlanAgentPromptAssets.ProjectPlanAgentPrompt);
        string verifierPrompt = _promptAssetReader.ReadRequiredPrompt(ProjectPlanAgentPromptAssets.ProjectVerifierAgentPrompt);

        Assert.Contains("Task items represent whole files only.", plannerPrompt);
        Assert.Contains("it must become a single-file task item", plannerPrompt);
        Assert.Contains("Task items represent whole files, so do not require the planner to split a file into line ranges.", verifierPrompt);
        Assert.Contains("Do not reject a plan merely because later review will need multiple bounded `ReadFileRange` calls", verifierPrompt);
    }

    [TestMethod]
    public void RuleReviewAgent_RendersRuleReviewPlaceholders()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new RuleReviewAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            new RuleReviewIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            RenderRuleScopePrompt(RuleReviewAgentPromptAssets.RuleReviewAgentPrompt, ruleMarkdown, taskItem),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ReviewVerifier_RendersRuleReviewPlaceholders()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new ReviewVerifierFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            new RuleReviewIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            RenderRuleScopePrompt(RuleReviewAgentPromptAssets.ReviewVerifierAgentPrompt, ruleMarkdown, taskItem),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ReportAggregator_RendersRuleScopePlaceholders()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new ReportAggregatorAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            new ReportIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            RenderRuleScopePrompt(ReportAgentPromptAssets.ReportAggregatorAgentPrompt, ruleMarkdown, taskItem),
            result.SystemPrompt);
    }

    [TestMethod]
    public void ReportVerifier_RendersCurrentFlowIssuesJsonPlaceholder()
    {
        StoredTaskItem taskItem = CreateTaskItem();
        IReadOnlyList<StoredIssue> currentFlowIssues = [CreateRuleReviewIssue()];
        string ruleMarkdown = "# Rule";

        AgentCreationResult result = new ReportVerifierAgentFactory(CreateCompactionOptions()).Create(
            new NoOpChatClient(),
            AppContext.BaseDirectory,
            "rule-key",
            ruleMarkdown,
            taskItem,
            currentFlowIssues,
            new ReportIssueStore(),
            new ReviewVerdictBuffer());

        Assert.AreEqual(
            Render(
                ReportAgentPromptAssets.ReportVerifierAgentPrompt,
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
        StoredTaskItem taskItem) =>
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

    private static AgentCompactionOptions CreateCompactionOptions() =>
        new()
        {
            Reducer = new ChatReducer(
                new CompactionOptions
                {
                    ModelContextWindowTokens = 100_000,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                },
                new StaticSummaryPromptProvider("Summarize."),
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

    private static StoredTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-item",
            Files =
            [
                new PlanFile
                {
                    FilePath = "src/Project/Program.cs",
                    TotalLines = 42,
                },
            ],
        };

    private static StoredIssue CreateRuleReviewIssue() =>
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

    private sealed class StaticSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
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
