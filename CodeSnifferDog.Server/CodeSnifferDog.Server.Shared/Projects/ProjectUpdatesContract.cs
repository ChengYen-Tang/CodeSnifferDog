namespace CodeSnifferDog.Server.Shared.Projects;

/// <summary>
/// Defines the SignalR contract used to publish project and agent-status updates.
/// </summary>
public static class ProjectUpdatesContract
{
    /// <summary>
    /// Gets the SignalR hub path for project updates.
    /// </summary>
    public const string HubPath = "/hubs/projects";

    /// <summary>
    /// Gets the client method name for project list refresh notifications.
    /// </summary>
    public const string ProjectsChangedMethodName = "ProjectsChanged";

    /// <summary>
    /// Gets the server method name that subscribes to project-level updates.
    /// </summary>
    public const string SubscribeToProjectMethodName = "SubscribeToProject";

    /// <summary>
    /// Gets the server method name that unsubscribes from project-level updates.
    /// </summary>
    public const string UnsubscribeFromProjectMethodName = "UnsubscribeFromProject";

    /// <summary>
    /// Gets the server method name that subscribes to an agent timeline stream.
    /// </summary>
    public const string SubscribeToAgentTimelineMethodName = "SubscribeToAgentTimeline";

    /// <summary>
    /// Gets the server method name that unsubscribes from an agent timeline stream.
    /// </summary>
    public const string UnsubscribeFromAgentTimelineMethodName = "UnsubscribeFromAgentTimeline";

    /// <summary>
    /// Gets the client method name for agent-status live updates.
    /// </summary>
    public const string AgentStatusUpdatedMethodName = "AgentStatusUpdated";

    /// <summary>
    /// Gets the channel name used for project-level broadcasts.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <returns>The project channel name.</returns>
    public static string GetProjectChannelName(Guid projectId) => $"project:{projectId:N}";

    /// <summary>
    /// Gets the channel name used for project-agent timeline broadcasts.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <param name="agentId">Agent identifier.</param>
    /// <returns>The project-agent channel name.</returns>
    public static string GetProjectAgentChannelName(Guid projectId, Guid agentId) => $"project:{projectId:N}:agent:{agentId:N}";
}
