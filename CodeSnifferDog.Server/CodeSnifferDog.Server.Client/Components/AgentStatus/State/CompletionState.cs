using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Holds the derived completion badge text, style, and detail message shown by the agent-status page.
/// </summary>
/// <param name="badgeText">Primary badge text shown by the UI.</param>
/// <param name="badgeCss">Badge CSS class shown by the UI.</param>
/// <param name="detailText">Longer descriptive text shown by the UI.</param>
internal sealed class CompletionState(string badgeText, string badgeCss, string detailText)
{
    /// <summary>
    /// Gets the primary badge text shown by the UI.
    /// </summary>
    public string BadgeText { get; } = badgeText;

    /// <summary>
    /// Gets the badge CSS class shown by the UI.
    /// </summary>
    public string BadgeCss { get; } = badgeCss;

    /// <summary>
    /// Gets the longer descriptive text shown by the UI.
    /// </summary>
    public string DetailText { get; } = detailText;

    /// <summary>
    /// Derives completion badge state from the project execution status and live connection state.
    /// </summary>
    /// <param name="projectStatus">Current project execution status, when known.</param>
    /// <param name="liveConnection">Current live connection state used to distinguish active and disconnected reviewing states.</param>
    /// <returns>The derived completion badge state.</returns>
    public static CompletionState From(ProjectStatus? projectStatus, LiveConnectionState liveConnection)
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
