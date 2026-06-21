using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Queries;

internal sealed record ProjectSidebarSnapshotReadModel(
    Guid? SelectedProjectId,
    IReadOnlyList<ProjectSidebarGroupReadModel> Groups);

internal sealed record ProjectSidebarGroupReadModel(
    string GroupKey,
    string DisplayName,
    ProjectStatus Status,
    int SortOrder,
    IReadOnlyList<ProjectSidebarProjectReadModel> Projects);

internal sealed record ProjectSidebarProjectReadModel(
    ProjectSidebarProjectProjection Project,
    ProjectStatus Status,
    int SortOrder);
