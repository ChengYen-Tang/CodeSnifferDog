using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CodeSnifferDog.Agents.Report;

public sealed class ReportVerifierAgentFactory(
    OperationalContextAgentCompactionOptions compactionOptions,
    PromptAssetReader? promptAssetReader = null,
    PromptTemplateRenderer? promptTemplateRenderer = null,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly OperationalContextAgentCompactionOptions _compactionOptions = compactionOptions;
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly PromptTemplateRenderer _promptTemplateRenderer = promptTemplateRenderer ?? new();
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    public AIAgent Create(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        Create(
            chatClient,
            _promptAssetReader.ReadRequiredPrompt(ReportPromptAssetPaths.ReportVerifierAgentPrompt),
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            currentFlowIssues,
            reportIssueStore,
            verdictBuffer);

    public AIAgent Create(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer)
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

        CommonToolSet commonToolSet = new(repositoryRootPath);
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(repositoryRootPath, ruleKey);
        ReportToolSet toolSet = new(reportIssueStore, verdictBuffer, ruleFlowKey, ruleReportKey);
        AIAgent agent = chatClient.AsAIAgent(
            RenderPrompt(promptTemplate, repositoryRootPath, ruleMarkdown, currentFlowIssues),
            "Report Verifier Agent",
            "Verifies whether the current repository-level aggregation diff is acceptable.",
            [.. commonToolSet.CreateTools(), .. toolSet.CreateVerifierTools()],
            _loggerFactory,
            _serviceProvider);

        return new AIAgentBuilder(agent)
            .UseOperationalContextCompaction(_compactionOptions)
            .Build(_serviceProvider);
    }

    private string RenderPrompt(
        string promptTemplate,
        string repositoryRootPath,
        string ruleMarkdown,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues)
        =>
        _promptTemplateRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
                ["RuleMarkdown"] = ruleMarkdown,
                ["CurrentFlowIssuesJson"] = JsonSerializer.Serialize(currentFlowIssues),
            });
}
