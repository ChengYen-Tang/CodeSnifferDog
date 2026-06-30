using CodeSnifferDog.Server.Client.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Layout.Navigation;

internal static class ProjectSidebarProjectionBuilder
{
    public static IReadOnlyList<ProjectGroup> CreateGroups(ProjectSidebarState state) =>
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
                        CreateActions(project.ProjectId, project.Status)))
                    .ToList(),
            })
            .ToList();

    private static IReadOnlyList<ProjectAction> CreateActions(Guid projectId, ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing =>
        [
            ProjectAction.Link("S", "Agent Team / Worker Status", $"/agent-status?projectId={projectId}"),
            ProjectAction.Cancel(),
        ],
        ProjectStatus.Completed =>
        [
            ProjectAction.Link("S", "Agent Team / Worker Status", $"/agent-status?projectId={projectId}"),
            ProjectAction.Link("R", "Report", $"/reports/{projectId}"),
            ProjectAction.Delete(),
        ],
        _ =>
        [
            ProjectAction.Delete(),
        ],
    };

    private static string GetGroupIconText(ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing => "R",
        ProjectStatus.Completed => "C",
        ProjectStatus.Queued => "Q",
        ProjectStatus.Failed => "F",
        ProjectStatus.Canceled => "C",
        _ => "?",
    };

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
