using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.Report;

public sealed class ReportVerifierAgentFactory(
    OperationalContextAgentCompactionOptions compactionOptions,
    PromptAssetReader? promptAssetReader = null,
    PromptTemplateRenderer? promptTemplateRenderer = null,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly AgentPromptRenderer _promptRenderer = new(promptAssetReader, promptTemplateRenderer);
    private readonly AgentToolComposer _toolComposer = new(loggerFactory);
    private readonly AgentBuilderService _agentBuilderService = new(compactionOptions, loggerFactory, serviceProvider);

    public AgentCreationResult Create(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope = null) =>
        CreateFromPromptTemplate(
            chatClient,
            _promptRenderer.ReadRequiredPrompt(ReportPromptAssetPaths.ReportVerifierAgentPrompt),
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            currentFlowIssues,
            reportIssueStore,
            verdictBuffer,
            eventScope);

    private AgentCreationResult CreateFromPromptTemplate(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleMarkdown);
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(currentFlowIssues);
        ArgumentNullException.ThrowIfNull(reportIssueStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        string systemPrompt = _promptRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
                ["RuleMarkdown"] = ruleMarkdown,
                ["CurrentFlowIssuesJson"] = AgentPromptRenderer.JsonValue(currentFlowIssues),
            });
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(repositoryRootPath, ruleKey);
        ReportToolSet toolSet = new(reportIssueStore, verdictBuffer, ruleFlowKey, ruleReportKey);
        return _agentBuilderService.Create(new AgentBuildRequest(
            chatClient,
            systemPrompt,
            "Report Verifier Agent",
            "Verifies whether the current repository-level aggregation diff is acceptable.",
            _toolComposer.Compose(repositoryRootPath, toolSet.CreateVerifierTools()),
            eventScope));
    }
}
