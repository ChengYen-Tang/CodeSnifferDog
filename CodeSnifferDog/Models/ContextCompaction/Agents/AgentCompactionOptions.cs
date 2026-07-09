using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Models.ContextCompaction.Agents;

/// <summary>
/// Configures transcript compaction behavior for agents built on the agent framework adapter.
/// </summary>
public sealed class AgentCompactionOptions
{
    /// <summary>
    /// Gets the reducer used to summarize and compact chat transcripts.
    /// </summary>
    public required ChatReducer Reducer { get; init; }

    /// <summary>
    /// Gets the optional collapse controller used for context-collapse mode.
    /// </summary>
    public CollapseController? CollapseController { get; init; }

    /// <summary>
    /// Gets the shrinker used for local message shrinking before full compaction.
    /// </summary>
    public MessageShrinker MessageShrinker { get; init; } = new();

    /// <summary>
    /// Gets whether reactive compaction retries are enabled after model invocation failures.
    /// </summary>
    public bool EnableReactiveCompactionRetry { get; init; } = true;

    /// <summary>
    /// Gets the classifier used to decide whether a model exception should trigger reactive compaction.
    /// </summary>
    public IReactiveExceptionDecider ReactiveExceptionDecider { get; init; } =
        new DefaultReactiveExceptionDecider();

    /// <summary>
    /// Gets the optional logger factory used by compaction adapters.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }
}
