namespace CodeSnifferDog.Server.Shared.Projects;

public static class ProjectUpdatesContract
{
    public const string HubPath = "/hubs/projects";

    public const string ProjectsChangedMethodName = "ProjectsChanged";

    public const string SubscribeToProjectMethodName = "SubscribeToProject";

    public const string UnsubscribeFromProjectMethodName = "UnsubscribeFromProject";

    public const string SubscribeToAgentTimelineMethodName = "SubscribeToAgentTimeline";

    public const string UnsubscribeFromAgentTimelineMethodName = "UnsubscribeFromAgentTimeline";

    public const string AgentStatusUpdatedMethodName = "AgentStatusUpdated";

    public static string GetProjectChannelName(Guid projectId) => $"project:{projectId:N}";

    public static string GetProjectAgentChannelName(Guid projectId, Guid agentId) => $"project:{projectId:N}:agent:{agentId:N}";
}
