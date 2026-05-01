using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
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

public sealed class ReportAggregatorAgentFactory(
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
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        Create(
            chatClient,
            _promptAssetReader.ReadRequiredPrompt(ReportPromptAssetPaths.ReportAggregatorAgentPrompt),
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            reportIssueStore,
            verdictBuffer);

    public AIAgent Create(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleMarkdown);
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(reportIssueStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        CommonToolSet commonToolSet = new(repositoryRootPath);
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(repositoryRootPath, ruleKey);
        ReportToolSet toolSet = new(reportIssueStore, verdictBuffer, ruleFlowKey, ruleReportKey);
        AIAgent agent = chatClient.AsAIAgent(
            RenderPrompt(promptTemplate, repositoryRootPath, ruleMarkdown, taskItem),
            "Report Aggregator Agent",
            "Merges verified flow issues into the repository-level issue set for one rule.",
            [.. commonToolSet.CreateTools(), .. toolSet.CreateReportAggregatorTools()],
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
        StoredProjectPlanTaskItem taskItem)
        =>
        _promptTemplateRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
                ["RuleMarkdown"] = ruleMarkdown,
                ["ScopeFilesJson"] = JsonSerializer.Serialize(taskItem.Files),
            });
}
