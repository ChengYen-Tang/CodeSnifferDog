using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.Projects.Queries;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects;

internal sealed class ProjectSidebarSnapshotService(
    IProjectSidebarQueryService queryService,
    IProjectProjectionMapper projectionMapper) : IProjectSidebarSnapshotService
{
    private readonly IProjectSidebarQueryService _queryService = queryService;
    private readonly IProjectProjectionMapper _projectionMapper = projectionMapper;

    public async Task<ProjectSidebarSnapshotDto> GetSnapshotAsync(Guid? selectedProjectId, CancellationToken cancellationToken = default)
    {
        ProjectSidebarSnapshotReadModel snapshot = await _queryService
            .GetSnapshotAsync(selectedProjectId, cancellationToken)
            .ConfigureAwait(false);

        return new ProjectSidebarSnapshotDto
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SelectedProjectId = snapshot.SelectedProjectId,
            Groups = snapshot.Groups.Select(MapGroup).ToList(),
        };
    }

    private ProjectSidebarGroupDto MapGroup(ProjectSidebarGroupReadModel group) => new()
    {
        GroupKey = group.GroupKey,
        DisplayName = group.DisplayName,
        Status = group.Status,
        SortOrder = group.SortOrder,
        Projects = group.Projects
            .Select(project => _projectionMapper.MapSidebarProject(project.Project, project.Status, project.SortOrder))
            .ToList(),
    };
}
