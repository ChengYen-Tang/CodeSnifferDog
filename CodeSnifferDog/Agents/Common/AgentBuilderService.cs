using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Agents.Common;

internal sealed class AgentBuilderService(
    AgentCompactionOptions compactionOptions,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly AgentCompactionOptions _compactionOptions = compactionOptions;
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    public AgentCreationResult Create(AgentBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ChatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);
        ArgumentNullException.ThrowIfNull(request.Tools);

        AIAgent agent = request.ChatClient.AsAIAgent(
            request.SystemPrompt,
            request.Name,
            request.Description,
            request.Tools,
            _loggerFactory,
            _serviceProvider);

        return new AgentCreationResult
        {
            Agent = new AIAgentBuilder(agent)
                .UseOperationalContextCompaction(_compactionOptions)
                .UseAgentTranscriptEventsIfAvailable(request.EventScope)
                .Build(_serviceProvider),
            SystemPrompt = request.SystemPrompt,
        };
    }
}

internal sealed record AgentBuildRequest(
    IChatClient ChatClient,
    string SystemPrompt,
    string Name,
    string Description,
    IList<AITool> Tools,
    IAgentEventScope? EventScope);
