using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.Scan;

public sealed class ScanVerifierAgentFactory(
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

    public AgentCreationResult Create(
        IChatClient chatClient,
        string repositoryRootPath,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer) =>
        CreateFromPromptTemplate(
            chatClient,
            _promptAssetReader.ReadRequiredPrompt(ScanPromptAssetPaths.ScanVerifierAgentPrompt),
            repositoryRootPath,
            scanProjectStore,
            verdictBuffer);

    private AgentCreationResult CreateFromPromptTemplate(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(scanProjectStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        string systemPrompt = RenderPrompt(promptTemplate, repositoryRootPath);
        CommonToolSet commonToolSet = new(repositoryRootPath);
        ScanToolSet toolSet = new(scanProjectStore, verdictBuffer);
        AIAgent agent = chatClient.AsAIAgent(
            systemPrompt,
            "Scan Verifier Agent",
            "Verifies whether the current scan result is acceptable for the planning stage.",
            [.. commonToolSet.CreateTools(), .. toolSet.CreateVerifierTools()],
            _loggerFactory,
            _serviceProvider);

        return new AgentCreationResult
        {
            Agent = new AIAgentBuilder(agent)
                .UseOperationalContextCompaction(_compactionOptions)
                .Build(_serviceProvider),
            SystemPrompt = systemPrompt,
        };
    }

    private string RenderPrompt(string promptTemplate, string repositoryRootPath)
        =>
        _promptTemplateRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
            });
}
