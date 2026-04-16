using System.Text.Json;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.ProjectPlan;

public sealed class ProjectVerifierAgentFactory(
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
        StoredScanProject scanProject,
        IProjectPlanTaskItemStore taskItemStore,
        ReviewVerdictBuffer verdictBuffer) =>
        Create(
            chatClient,
            _promptAssetReader.ReadRequiredPrompt(ProjectPlanPromptAssetPaths.ProjectVerifierAgentPrompt),
            repositoryRootPath,
            scanProject,
            taskItemStore,
            verdictBuffer);

    public AIAgent Create(
        IChatClient chatClient,
        string promptTemplate,
        string repositoryRootPath,
        StoredScanProject scanProject,
        IProjectPlanTaskItemStore taskItemStore,
        ReviewVerdictBuffer verdictBuffer)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(scanProject);
        ArgumentNullException.ThrowIfNull(taskItemStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        CommonToolSet commonToolSet = new(repositoryRootPath);
        ProjectPlanToolSet toolSet = new(taskItemStore, verdictBuffer);
        AIAgent agent = chatClient.AsAIAgent(
            RenderPrompt(promptTemplate, repositoryRootPath, scanProject),
            "Project Verifier Agent",
            "Verifies whether the current project plan result is acceptable.",
            [.. commonToolSet.CreateTools(), .. toolSet.CreateVerifierTools()],
            _loggerFactory,
            _serviceProvider);

        return new AIAgentBuilder(agent)
            .UseOperationalContextCompaction(_compactionOptions)
            .Build(_serviceProvider);
    }

    private string RenderPrompt(string promptTemplate, string repositoryRootPath, StoredScanProject scanProject)
        =>
        _promptTemplateRenderer.Render(
            promptTemplate,
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = repositoryRootPath,
                ["ScanProjectJson"] = JsonSerializer.Serialize(scanProject),
            });
}
