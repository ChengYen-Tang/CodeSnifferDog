using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Agents.Scan;

public sealed class ScanAgentFactory(
    OperationalContextAgentCompactionOptions compactionOptions,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly OperationalContextAgentCompactionOptions _compactionOptions = compactionOptions;
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    public AIAgent Create(
        IChatClient chatClient,
        string promptTemplate,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTemplate);
        ArgumentNullException.ThrowIfNull(scanProjectStore);
        ArgumentNullException.ThrowIfNull(verdictBuffer);

        ScanToolSet toolSet = new(scanProjectStore, verdictBuffer);
        AIAgent agent = chatClient.AsAIAgent(
            promptTemplate,
            "Scan Agent",
            "Scans a repository and records project units for the planning stage.",
            toolSet.CreateScanAgentTools(),
            _loggerFactory,
            _serviceProvider);

        return new AIAgentBuilder(agent)
            .UseOperationalContextCompaction(_compactionOptions)
            .Build(_serviceProvider);
    }
}
