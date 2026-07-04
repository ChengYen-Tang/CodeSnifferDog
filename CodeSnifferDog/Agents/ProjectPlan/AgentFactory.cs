using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Agents.ProjectPlan;

/// <summary>
/// Creates the project-plan agent that turns one scanned project into review task items.
/// </summary>
/// <param name="compactionOptions">Compaction options applied to created agents.</param>
/// <param name="promptAssetReader">Optional prompt reader used to load prompt assets.</param>
/// <param name="promptTemplateRenderer">Optional template renderer used to inject repository placeholders.</param>
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
    /// Creates a project-plan agent from the default planner prompt asset.
    /// </summary>
    /// <param name="chatClient">Chat client that backs the created agent.</param>
    /// <param name="repositoryRootPath">Repository root path that contains the scanned project.</param>
    /// <param name="taskItemStore">Store that receives project-plan task item submissions.</param>
    /// <param name="verdictBuffer">Verdict buffer used by review-related tools.</param>
    /// <param name="eventScope">Optional event scope used to publish transcript events.</param>
    /// <returns>The created agent result.</returns>
    public AgentCreationResult Create(
        IChatClient chatClient,
        string repositoryRootPath,
        ITaskItemStore taskItemStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope = null) =>
        CreateFromPromptTemplate(
            chatClient,
            _promptRenderer.ReadRequiredPrompt(PromptAssetPaths.ProjectPlanAgentPrompt),
            repositoryRootPath,
            taskItemStore,
            verdictBuffer,
            eventScope);

    /// <summary>
    /// Creates a project-plan agent from one explicit prompt template.
    /// </summary>
    /// <param name="chatClient">Chat client that backs the created agent.</param>
    /// <param name="promptTemplate">Prompt template used to build the planner system prompt.</param>
    /// <param name="repositoryRootPath">Repository root path that contains the scanned project.</param>
    /// <param name="taskItemStore">Store that receives project-plan task item submissions.</param>
    /// <param name="verdictBuffer">Verdict buffer used by review-related tools.</param>
    /// <param name="eventScope">Optional event scope used to publish transcript events.</param>
    /// <returns>The created agent result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chatClient" />, <paramref name="taskItemStore" />, or <paramref name="verdictBuffer" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="promptTemplate" /> or <paramref name="repositoryRootPath" /> is null, empty, or whitespace.</exception>
    private AgentCreationResult CreateFromPromptTemplate(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        ITaskItemStore taskItemStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(taskItemStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        string systemPrompt = _promptRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
            });
        ToolSet toolSet = new(taskItemStore, verdictBuffer);
        return _agentBuilderService.Create(new AgentBuildRequest(
            chatClient,
            systemPrompt,
            "Project Plan Agent",
            "Creates review task items for one scanned project.",
            _toolComposer.Compose(repositoryRootPath, toolSet.CreateProjectPlanAgentTools()),
            eventScope));
    }
}
