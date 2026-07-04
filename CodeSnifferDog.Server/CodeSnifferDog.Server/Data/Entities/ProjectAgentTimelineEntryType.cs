namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Represents the type of a persisted agent timeline entry.
/// </summary>
public enum ProjectAgentTimelineEntryType
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
    /// Entry created for a tool call.
    /// </summary>
    Tool = 2,

    /// <summary>
    /// Entry created when context compaction occurs.
    /// </summary>
    Compaction = 3,
}
