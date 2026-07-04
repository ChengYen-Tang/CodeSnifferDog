using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;

/// <summary>
/// Read model that represents the full projects sidebar snapshot before DTO projection.
/// </summary>
/// <param name="SelectedProjectId">Resolved selected project identifier, when one exists.</param>
/// <param name="Groups">Grouped sidebar read models.</param>
internal sealed record SnapshotReadModel(
    Guid? SelectedProjectId,
    IReadOnlyList<GroupReadModel> Groups);

/// <summary>
/// Read model that represents one grouped section in the projects sidebar.
/// </summary>
/// <param name="GroupKey">Stable group key.</param>
/// <param name="DisplayName">Display name shown to the client.</param>
/// <param name="Status">Shared status represented by the group.</param>
/// <param name="SortOrder">Fixed group sort order.</param>
/// <param name="Projects">Projects inside the group.</param>
internal sealed record GroupReadModel(
    string GroupKey,
    string DisplayName,
    ProjectStatus Status,
    int SortOrder,
    IReadOnlyList<ProjectReadModel> Projects);

/// <summary>
/// Read model that represents one project row inside a sidebar group.
/// </summary>
/// <param name="Project">Projected project fields.</param>
/// <param name="Status">Mapped shared project status.</param>
/// <param name="SortOrder">Sort order inside the group.</param>
internal sealed record ProjectReadModel(
    ProjectProjection Project,
    ProjectStatus Status,
    int SortOrder);
