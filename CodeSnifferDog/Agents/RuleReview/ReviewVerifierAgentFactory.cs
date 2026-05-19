using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.RuleReview;

public sealed class ReviewVerifierAgentFactory(
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
        IRuleReviewIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        Create(
            chatClient,
            _promptAssetReader.ReadRequiredPrompt(RuleReviewPromptAssetPaths.ReviewVerifierAgentPrompt),
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            issueStore,
            verdictBuffer);

    public AIAgent Create(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IRuleReviewIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleMarkdown);
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(issueStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        CommonToolSet commonToolSet = new(repositoryRootPath);
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        RuleReviewToolSet toolSet = new(issueStore, verdictBuffer, ruleFlowKey);
        AIAgent agent = chatClient.AsAIAgent(
            RenderPrompt(promptTemplate, repositoryRootPath, ruleMarkdown, taskItem),
            "Review Verifier Agent",
            "Verifies whether the current rule review result is acceptable.",
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
        StoredProjectPlanTaskItem taskItem)
        =>
        _promptTemplateRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
                ["RuleMarkdown"] = ruleMarkdown,
                ["ScopeFilesJson"] = CodeSnifferDogJson.Serialize(taskItem.Files),
            });
}
