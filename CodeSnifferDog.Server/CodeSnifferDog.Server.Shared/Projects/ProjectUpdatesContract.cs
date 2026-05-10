namespace CodeSnifferDog.Server.Shared.Projects;

public static class ProjectUpdatesContract
{
    public const string HubPath = "/hubs/projects";

    public const string ProjectsChangedMethodName = "ProjectsChanged";

    public const string SubscribeToProjectMethodName = "SubscribeToProject";

    public const string UnsubscribeFromProjectMethodName = "UnsubscribeFromProject";

    public const string AgentStatusUpdatedMethodName = "AgentStatusUpdated";

    public static string GetProjectChannelName(Guid projectId) => $"project:{projectId:N}";
}
