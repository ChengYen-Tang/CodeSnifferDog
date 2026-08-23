using Microsoft.Agents.AI.Workflows;

namespace CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;

/// <summary>
/// Configures the Agent Framework execution boundary for one legacy workflow invocation.
/// </summary>
internal sealed class WorkflowRunOptions
{
    /// <summary>
    /// Gets the optional checkpoint manager used to checkpoint the executor boundary.
    /// </summary>
    public CheckpointManager? CheckpointManager { get; init; }

    /// <summary>
    /// Gets the optional session identifier associated with the execution.
    /// </summary>
    public string? SessionId { get; init; }
}
