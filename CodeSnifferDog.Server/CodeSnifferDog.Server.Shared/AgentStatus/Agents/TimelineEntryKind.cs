namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents the kind of an agent timeline entry.
/// </summary>
public enum TimelineEntryKind
{
    /// <summary>
    /// Entry created from user input.
    /// </summary>
    Input = 0,

    /// <summary>
    /// Entry created from assistant output.
    /// </summary>
    Output = 1,

    /// <summary>
    /// Entry created from a tool call.
    /// </summary>
    Tool = 2,

    /// <summary>
    /// Entry created when context compaction occurs.
    /// </summary>
    Compaction = 3,
}
