using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusCompletionState(string badgeText, string badgeCss, string detailText)
{
    public string BadgeText { get; } = badgeText;

    public string BadgeCss { get; } = badgeCss;

    public string DetailText { get; } = detailText;

    public static AgentStatusCompletionState From(ProjectStatus? projectStatus, AgentStatusLiveConnectionState liveConnection)
    {
        return projectStatus switch
        {
            ProjectStatus.Completed => new("Analysis completed", "text-bg-success", "Project execution finished successfully."),
            ProjectStatus.Failed => new("Analysis failed", "text-bg-danger", "Project execution ended with a failure."),
            ProjectStatus.Canceled => new("Analysis canceled", "text-bg-secondary", "Project execution was canceled."),
            ProjectStatus.Reviewing when liveConnection.IsConnected && liveConnection.IsSubscribed =>
                new("Analysis running", "text-bg-primary", "Live updates are connected."),
            ProjectStatus.Reviewing when string.Equals(liveConnection.StatusText, "Live pending", StringComparison.Ordinal) =>
                new("Analysis running", "text-bg-primary", "Live updates will connect when the client becomes interactive."),
            ProjectStatus.Reviewing =>
                new("Live disconnected", "text-bg-warning", "Analysis is still running, but live updates are currently unavailable."),
            ProjectStatus.Queued => new("Analysis queued", "text-bg-secondary", "Project is waiting for execution."),
            _ => new("Status unavailable", "text-bg-secondary", "Project execution state is not available yet."),
        };
    }
}