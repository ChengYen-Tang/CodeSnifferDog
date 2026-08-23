using Microsoft.Agents.AI.Workflows;

namespace CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;

/// <summary>
/// Holds the domain output and Agent Framework execution evidence for one workflow invocation.
/// </summary>
/// <typeparam name="TOutput">Type returned by the wrapped domain workflow.</typeparam>
internal sealed record WorkflowRunResult<TOutput>(
    TOutput Output,
    IReadOnlyList<WorkflowEvent> Events,
    IReadOnlyList<CheckpointInfo> Checkpoints);
