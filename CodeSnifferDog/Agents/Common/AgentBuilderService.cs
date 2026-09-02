using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Agents.Common.TokenUsage;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Agents.Common;

/// <summary>
/// Creates configured <see cref="AIAgent" /> instances and wraps them with transcript and compaction behaviors.
/// </summary>
/// <param name="compactionOptions">Compaction options applied to created agents.</param>
/// <param name="loggerFactory">Optional logger factory forwarded to agent creation.</param>
/// <param name="serviceProvider">Optional service provider used by the agent builder pipeline.</param>
internal sealed class AgentBuilderService(
    AgentCompactionOptions compactionOptions,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly AgentCompactionOptions _compactionOptions = compactionOptions;
    private readonly ILoggerFactory? _loggerFactory = loggerFactory;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    /// <summary>
    /// Creates an agent from one normalized build request.
    /// </summary>
    /// <param name="request">Build request that describes the chat client, prompt, identity, tools, and optional event scope.</param>
    /// <returns>The created agent together with the system prompt used to construct it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" />, <see cref="AgentBuildRequest.ChatClient" />, or <see cref="AgentBuildRequest.Tools" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><see cref="AgentBuildRequest.SystemPrompt" />, <see cref="AgentBuildRequest.Name" />, or <see cref="AgentBuildRequest.Description" /> is null, empty, or whitespace.</exception>
    public AgentCreationResult Create(AgentBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ChatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);
        ArgumentNullException.ThrowIfNull(request.Tools);

        IServiceProvider? agentServices = GetAgentServices();
        ChatClientBuilder chatClientBuilder = request.ChatClient.AsBuilder();
        if (_compactionOptions.Reducer.Options.Mode == CompactionMode.Standard)
        {
            // Keep CodeSnifferDog's compaction adapter below the framework-managed
            // FunctionInvokingChatClient so each function-loop iteration still reaches
            // the established compaction module.
            chatClientBuilder.Use(innerClient =>
                new CompactingChatClient(innerClient, _compactionOptions, _loggerFactory));
        }

        ChatClientAgent agent = chatClientBuilder.BuildAIAgent(
            new ChatClientAgentOptions
            {
                Name = request.Name,
                Description = request.Description,
                ChatOptions = new ChatOptions
                {
                    Instructions = request.SystemPrompt,
                    Tools = request.Tools,
                    ModelId = ChatClientIdentity.TryGetModelId(request.ChatClient),
                },
            },
            _loggerFactory,
            agentServices);

        AIAgentBuilder agentBuilder = new(agent);
        if (_compactionOptions.Reducer.Options.Mode == CompactionMode.Standard)
            agentBuilder.UseOperationalContextCompactionRuntime(_compactionOptions);
        else
            agentBuilder.UseOperationalContextCompaction(_compactionOptions);

        return new AgentCreationResult
        {
            Agent = agentBuilder
                .UseAgentTranscriptEventsIfAvailable(request.EventScope)
                .Build(agentServices),
            SystemPrompt = request.SystemPrompt,
        };
    }

    private IServiceProvider? GetAgentServices() =>
        _loggerFactory is null
            ? _serviceProvider
            : new LoggerFactoryServiceProvider(_serviceProvider, _loggerFactory);

    /// <summary>
    /// Makes the explicitly supplied logger factory visible to Agent Framework middleware
    /// while forwarding all other service resolution to the composition-root provider.
    /// </summary>
    private sealed class LoggerFactoryServiceProvider(
        IServiceProvider? inner,
        ILoggerFactory loggerFactory) : IServiceProvider, IKeyedServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ILoggerFactory)
                ? loggerFactory
                : inner?.GetService(serviceType);

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            serviceKey is null
                ? GetService(serviceType)
                : inner is IKeyedServiceProvider keyedServices
                    ? keyedServices.GetKeyedService(serviceType, serviceKey)
                    : null;

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            GetKeyedService(serviceType, serviceKey)
            ?? throw new InvalidOperationException(
                $"No service for type '{serviceType}' and key '{serviceKey}' has been registered.");
    }
}

/// <summary>
/// Describes the inputs required to build one configured agent instance.
/// </summary>
/// <param name="ChatClient">Chat client that backs the created agent.</param>
/// <param name="SystemPrompt">System prompt supplied to the agent.</param>
/// <param name="Name">Agent name.</param>
/// <param name="Description">Agent description.</param>
/// <param name="Tools">Tools exposed to the agent.</param>
/// <param name="EventScope">Optional event scope used to publish transcript events.</param>
internal sealed record AgentBuildRequest(
    IChatClient ChatClient,
    string SystemPrompt,
    string Name,
    string Description,
    IList<AITool> Tools,
    IAgentEventScope? EventScope);
