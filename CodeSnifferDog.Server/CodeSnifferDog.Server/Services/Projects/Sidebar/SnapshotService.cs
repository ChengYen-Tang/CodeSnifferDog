using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar;

/// <summary>
/// Builds sidebar snapshots from query read models and projection mappers.
/// </summary>
/// <param name="queryService">Query service that loads sidebar read models.</param>
/// <param name="projectionMapper">Mapper that converts internal projections to shared DTOs.</param>
internal sealed class SnapshotService(
    IQueryService queryService,
    IProjectProjectionMapper projectionMapper) : ISnapshotService
{
    private readonly IQueryService _queryService = queryService;
    private readonly IProjectProjectionMapper _projectionMapper = projectionMapper;

    /// <inheritdoc />
    public async Task<ProjectSidebarSnapshotDto> GetSnapshotAsync(Guid? selectedProjectId, CancellationToken cancellationToken = default)
    {
        SnapshotReadModel snapshot = await _queryService
            .GetSnapshotAsync(selectedProjectId, cancellationToken)
            .ConfigureAwait(false);

        return new ProjectSidebarSnapshotDto
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SelectedProjectId = snapshot.SelectedProjectId,
            Groups = snapshot.Groups.Select(MapGroup).ToList(),
        };
    }

    /// <summary>
    /// Maps one sidebar group read model to the shared sidebar group DTO.
    /// </summary>
    /// <param name="group">Sidebar group read model.</param>
    /// <returns>The mapped sidebar group DTO.</returns>
    private ProjectSidebarGroupDto MapGroup(GroupReadModel group) => new()
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
