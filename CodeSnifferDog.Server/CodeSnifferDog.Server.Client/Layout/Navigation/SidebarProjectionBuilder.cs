using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Layout.Navigation;

/// <summary>
/// Projects sidebar state into navigation view models consumed by the Blazor layout.
/// </summary>
internal static class SidebarProjectionBuilder
{
    /// <summary>
    /// Builds grouped project navigation items from the sidebar state.
    /// </summary>
    /// <param name="state">Sidebar state to project.</param>
    /// <returns>The projected sidebar groups.</returns>
    public static IReadOnlyList<ProjectGroup> CreateGroups(State state) =>
        state.Snapshot.Groups
            .Select(group => new ProjectGroup(
                group.GroupKey,
                group.DisplayName,
                group.Status,
                GetGroupIconText(group.Status),
                GetGroupIconCssClass(group.Status),
                state.Ui.IsExpanded(group.GroupKey, group.Status))
            {
                Items = group.Projects
                    .Select(project => new ProjectItem(
                        project.ProjectId.ToString(),
                        project.OriginalFileName,
                        $"uploaded {project.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}",
                        CreateAgentStatusHref(project.ProjectId),
                        CreateActions(project.ProjectId, project.Status)))
                    .ToList(),
            })
            .ToList();

    /// <summary>
    /// Creates the action list for one project row based on its current status.
    /// </summary>
    /// <param name="projectId">Project identifier used by generated links.</param>
    /// <param name="status">Current project status.</param>
    /// <returns>The projected action list.</returns>
    private static IReadOnlyList<ProjectAction> CreateActions(Guid projectId, ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing =>
        [
            ProjectAction.Link("S", "Agent Team / Worker Status", CreateAgentStatusHref(projectId)),
            ProjectAction.Cancel(),
        ],
        ProjectStatus.Completed =>
        [
            ProjectAction.Link("S", "Agent Team / Worker Status", CreateAgentStatusHref(projectId)),
            ProjectAction.Link("R", "Report", $"/reports/{projectId}"),
            ProjectAction.Delete(),
        ],
        ProjectStatus.Failed or ProjectStatus.Canceled =>
        [
            ProjectAction.Link("S", "Agent Team / Worker Status", CreateAgentStatusHref(projectId)),
            ProjectAction.Delete(),
        ],
        _ =>
        [
            ProjectAction.Delete(),
        ],
    };

    /// <summary>
    /// Creates the agent-status route for a project.
    /// </summary>
    /// <param name="projectId">Project identifier used by the route.</param>
    /// <returns>The project-scoped agent-status route.</returns>
    private static string CreateAgentStatusHref(Guid projectId) => $"/agent-status?projectId={projectId}";

    /// <summary>
    /// Gets the short icon-like text used for one project-status group.
    /// </summary>
    /// <param name="status">Project status represented by the group.</param>
    /// <returns>The icon text.</returns>
    private static string GetGroupIconText(ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing => "R",
        ProjectStatus.Completed => "C",
        ProjectStatus.Queued => "Q",
        ProjectStatus.Failed => "F",
        ProjectStatus.Canceled => "C",
        _ => "?",
    };

    /// <summary>
    /// Gets the CSS class applied to one project-status group icon.
    /// </summary>
    /// <param name="status">Project status represented by the group.</param>
    /// <returns>The icon CSS class.</returns>
    private static string GetGroupIconCssClass(ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing => "group-icon-reviewing",
        ProjectStatus.Completed => "group-icon-completed",
        ProjectStatus.Queued => "group-icon-queued",
        ProjectStatus.Failed => "group-icon-failed",
        ProjectStatus.Canceled => "group-icon-canceled",
        _ => string.Empty,
    };
}
