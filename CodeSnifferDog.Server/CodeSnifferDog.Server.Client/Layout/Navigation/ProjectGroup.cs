using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Layout.Navigation;

/// <summary>
/// Represents one grouped section in the projects sidebar.
/// </summary>
/// <param name="groupKey">Stable group key.</param>
/// <param name="title">Display title shown for the group.</param>
/// <param name="status">Project status represented by the group.</param>
/// <param name="iconText">Short icon-like text shown for the group.</param>
/// <param name="iconCssClass">CSS class applied to the group icon.</param>
/// <param name="isExpanded">Whether the group is currently expanded.</param>
internal sealed class ProjectGroup(
    string groupKey,
    string title,
    ProjectStatus status,
    string iconText,
    string iconCssClass,
    bool isExpanded)
{
    /// <summary>
    /// Gets the stable group key.
    /// </summary>
    public string GroupKey { get; } = groupKey;

    /// <summary>
    /// Gets the display title shown for the group.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// Gets the project status represented by the group.
    /// </summary>
    public ProjectStatus Status { get; } = status;

    /// <summary>
    /// Gets the short icon-like text shown for the group.
    /// </summary>
    public string IconText { get; } = iconText;

    /// <summary>
    /// Gets the CSS class applied to the group icon.
    /// </summary>
    public string IconCssClass { get; } = iconCssClass;

    /// <summary>
    /// Gets whether the group is currently expanded.
    /// </summary>
    public bool IsExpanded { get; } = isExpanded;

    /// <summary>
    /// Gets the project items shown inside the group.
    /// </summary>
    public List<ProjectItem> Items { get; init; } = [];
}
