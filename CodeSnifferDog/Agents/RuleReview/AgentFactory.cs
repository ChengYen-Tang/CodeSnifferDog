using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Agents.RuleReview;

/// <summary>
/// Creates the rule-review agent that inspects one task-item scope under one rule.
/// </summary>
/// <param name="compactionOptions">Compaction options applied to created agents.</param>
/// <param name="promptAssetReader">Optional prompt reader used to load prompt assets.</param>
/// <param name="promptTemplateRenderer">Optional template renderer used to inject repository and scope placeholders.</param>
/// <param name="loggerFactory">Optional logger factory forwarded to agent construction and common tools.</param>
/// <param name="serviceProvider">Optional service provider used by the agent builder pipeline.</param>
public sealed class AgentFactory(
    AgentCompactionOptions compactionOptions,
    PromptAssetReader? promptAssetReader = null,
    PromptTemplateRenderer? promptTemplateRenderer = null,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly AgentPromptRenderer _promptRenderer = new(promptAssetReader, promptTemplateRenderer);
    private readonly AgentToolComposer _toolComposer = new(loggerFactory);
    private readonly AgentBuilderService _agentBuilderService = new(compactionOptions, loggerFactory, serviceProvider);

    /// <summary>
    /// Creates a rule-review agent from the default review prompt asset.
    /// </summary>
    /// <param name="chatClient">Chat client that backs the created agent.</param>
    /// <param name="repositoryRootPath">Repository root path that contains the reviewed code.</param>
    /// <param name="ruleKey">Rule key being reviewed.</param>
    /// <param name="ruleMarkdown">Rendered rule guidance supplied to the agent.</param>
    /// <param name="taskItem">Task item whose scope is being reviewed.</param>
    /// <param name="issueStore">Store that receives review issue submissions.</param>
    /// <param name="verdictBuffer">Verdict buffer used by review-related tools.</param>
    /// <param name="eventScope">Optional event scope used to publish transcript events.</param>
    /// <returns>The created agent result.</returns>
    public AgentCreationResult Create(
        IChatClient chatClient,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope = null) =>
        CreateFromPromptTemplate(
            chatClient,
            _promptRenderer.ReadRequiredPrompt(PromptAssetPaths.RuleReviewAgentPrompt),
            repositoryRootPath,
            ruleKey,
            ruleMarkdown,
            taskItem,
            issueStore,
            verdictBuffer,
            eventScope);

    /// <summary>
    /// Creates a rule-review agent from one explicit prompt template.
    /// </summary>
    /// <param name="chatClient">Chat client that backs the created agent.</param>
    /// <param name="promptTemplate">Prompt template used to build the review system prompt.</param>
    /// <param name="repositoryRootPath">Repository root path that contains the reviewed code.</param>
    /// <param name="ruleKey">Rule key being reviewed.</param>
    /// <param name="ruleMarkdown">Rendered rule guidance supplied to the agent.</param>
    /// <param name="taskItem">Task item whose scope is being reviewed.</param>
    /// <param name="issueStore">Store that receives review issue submissions.</param>
    /// <param name="verdictBuffer">Verdict buffer used by review-related tools.</param>
    /// <param name="eventScope">Optional event scope used to publish transcript events.</param>
    /// <returns>The created agent result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chatClient" />, <paramref name="taskItem" />, <paramref name="issueStore" />, or <paramref name="verdictBuffer" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="promptTemplate" />, <paramref name="repositoryRootPath" />, <paramref name="ruleKey" />, or <paramref name="ruleMarkdown" /> is null, empty, or whitespace.</exception>
    private AgentCreationResult CreateFromPromptTemplate(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleMarkdown);
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(issueStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        string systemPrompt = _promptRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
                ["RuleMarkdown"] = ruleMarkdown,
                ["ScopeFilesJson"] = AgentPromptRenderer.JsonValue(taskItem.Files),
            });
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        ToolSet toolSet = new(issueStore, verdictBuffer, ruleFlowKey);
        return _agentBuilderService.Create(new AgentBuildRequest(
            chatClient,
            systemPrompt,
            "Rule Review Agent",
            "Reviews one task-item scope under one rule and maintains discovered issues.",
            _toolComposer.Compose(repositoryRootPath, toolSet.CreateRuleReviewAgentTools()),
            eventScope));
    }
}
