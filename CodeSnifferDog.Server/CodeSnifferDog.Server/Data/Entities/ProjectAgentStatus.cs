namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Represents the persisted status of an execution agent.
/// </summary>
public enum ProjectAgentStatus
{
    /// <summary>
    /// The agent is waiting to run.
    /// </summary>
    Waiting = 0,

    /// <summary>
    /// The agent is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The agent completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The agent completed in a degraded state.
    /// </summary>
    Degraded = 3,
}
