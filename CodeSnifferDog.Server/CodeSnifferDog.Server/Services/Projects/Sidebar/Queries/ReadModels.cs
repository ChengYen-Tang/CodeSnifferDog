using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;

internal sealed record SnapshotReadModel(
    Guid? SelectedProjectId,
    IReadOnlyList<GroupReadModel> Groups);

internal sealed record GroupReadModel(
    string GroupKey,
    string DisplayName,
    ProjectStatus Status,
    int SortOrder,
    IReadOnlyList<ProjectReadModel> Projects);

internal sealed record ProjectReadModel(
    ProjectProjection Project,
    ProjectStatus Status,
    int SortOrder);
