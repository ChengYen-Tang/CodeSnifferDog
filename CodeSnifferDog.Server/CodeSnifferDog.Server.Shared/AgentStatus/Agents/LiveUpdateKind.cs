namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents the kind of live update sent to agent-status clients.
/// </summary>
public enum LiveUpdateKind
{
    /// <summary>
    /// An agent group was inserted or updated.
    /// </summary>
    AgentGroupUpserted = 1,

    /// <summary>
    /// An agent was inserted or updated.
    /// </summary>
    AgentUpserted = 2,

    /// <summary>
    /// An agent status changed.
    /// </summary>
    AgentStatusChanged = 3,

    /// <summary>
    /// A timeline entry was inserted or updated.
    /// </summary>
    TimelineEntryUpserted = 4,

    /// <summary>
    /// The project execution status changed.
    /// </summary>
    ProjectStatusChanged = 5,

    /// <summary>
    /// One or more timeline entries were removed.
    /// </summary>
    TimelineEntriesRemoved = 6,
}
