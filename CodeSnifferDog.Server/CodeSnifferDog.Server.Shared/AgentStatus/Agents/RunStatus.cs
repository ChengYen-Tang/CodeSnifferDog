namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents the user-facing run status of an agent.
/// </summary>
public enum RunStatus
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
