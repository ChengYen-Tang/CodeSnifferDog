using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.Scan;

public sealed class ScanAgentFactory(
    OperationalContextAgentCompactionOptions compactionOptions,
    PromptAssetReader? promptAssetReader = null,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly AgentPromptRenderer _promptRenderer = new(promptAssetReader);
    private readonly AgentToolComposer _toolComposer = new(loggerFactory);
    private readonly AgentBuilderService _agentBuilderService = new(compactionOptions, loggerFactory, serviceProvider);

    public AgentCreationResult Create(
        IChatClient chatClient,
        string repositoryRootPath,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope = null) =>
        CreateFromPromptTemplate(
            chatClient,
            _promptRenderer.ReadRequiredPrompt(ScanPromptAssetPaths.ScanAgentPrompt),
            repositoryRootPath,
            scanProjectStore,
            verdictBuffer,
            eventScope);

    private AgentCreationResult CreateFromPromptTemplate(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer,
        IAgentEventScope? eventScope)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(scanProjectStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        string systemPrompt = promptTemplate;
        ScanToolSet toolSet = new(scanProjectStore, verdictBuffer);
        return _agentBuilderService.Create(new AgentBuildRequest(
            chatClient,
            systemPrompt,
            "Scan Agent",
            "Scans a repository and records project units for the planning stage.",
            _toolComposer.Compose(repositoryRootPath, toolSet.CreateScanAgentTools()),
            eventScope));
    }
}
