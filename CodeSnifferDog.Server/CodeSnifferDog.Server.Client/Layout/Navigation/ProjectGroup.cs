using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Layout.Navigation;

internal sealed class ProjectGroup(
    string groupKey,
    string title,
    ProjectStatus status,
    string iconText,
    string iconCssClass,
    bool isExpanded)
{
    public string GroupKey { get; } = groupKey;

    public string Title { get; } = title;

    public ProjectStatus Status { get; } = status;

    public string IconText { get; } = iconText;

    public string IconCssClass { get; } = iconCssClass;

    public bool IsExpanded { get; } = isExpanded;

    public List<ProjectItem> Items { get; init; } = [];
}
