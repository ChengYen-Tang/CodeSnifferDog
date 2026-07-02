using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;

internal sealed class QueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectStatusMapper statusMapper) : IQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectStatusMapper _statusMapper = statusMapper;

    public async Task<SnapshotReadModel> GetSnapshotAsync(
        Guid? selectedProjectId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ProjectProjection> projectRows = await dbContext.Projects
            .AsNoTracking()
            .Select(project => new ProjectProjection(
                project.Id,
                project.OriginalFileName,
                project.Status,
                project.CreatedAtUtc,
                project.QueueTimestampUtc,
                project.FinishedAtUtc,
                project.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<MappedProject> projects = projectRows
            .Select(project => new MappedProject(
                project,
                _statusMapper.Map(project.Status),
                project.QueueTimestampUtc,
                project.FinishedAtUtc,
                project.UpdatedAtUtc))
            .ToList();

        List<GroupReadModel> groups = CreateGroupDefinitions()
            .Select(group => new GroupReadModel(
                group.GroupKey,
                group.DisplayName,
                group.Status,
                group.SortOrder,
                projects
                    .Where(project => project.Status == group.Status)
                    .OrderBy(GetProjectSortPrimary)
                    .ThenBy(project => project.Project.CreatedAtUtc)
                    .ThenBy(project => project.Project.ProjectId)
                    .Select((project, index) => new ProjectReadModel(
                        project.Project,
                        project.Status,
                        index))
                    .ToList()))
            .OrderBy(group => group.SortOrder)
            .ToList();

        Guid? resolvedSelectedProjectId = ResolveSelectedProjectId(selectedProjectId, groups);

        return new SnapshotReadModel(resolvedSelectedProjectId, groups);
    }

    private static IReadOnlyList<GroupDefinition> CreateGroupDefinitions() =>
    [
        new("reviewing", "Reviewing", ProjectStatus.Reviewing, 0),
        new("completed", "Completed", ProjectStatus.Completed, 1),
        new("queued", "Queued", ProjectStatus.Queued, 2),
        new("failed", "Failed", ProjectStatus.Failed, 3),
        new("canceled", "Canceled", ProjectStatus.Canceled, 4),
    ];

    private static DateTimeOffset GetProjectSortPrimary(MappedProject project) =>
        project.Status switch
        {
            ProjectStatus.Queued or ProjectStatus.Reviewing =>
                project.QueueTimestampUtc,
            _ => project.FinishedAtUtc ?? project.UpdatedAtUtc,
        };

    private static Guid? ResolveSelectedProjectId(
        Guid? requestedSelectedProjectId,
        IReadOnlyList<GroupReadModel> groups)
    {
        HashSet<Guid> projectIds = groups
            .SelectMany(group => group.Projects)
            .Select(project => project.Project.ProjectId)
            .ToHashSet();

        if (requestedSelectedProjectId is Guid selectedProjectId && projectIds.Contains(selectedProjectId))
            return selectedProjectId;

        return groups
            .OrderBy(group => group.SortOrder)
            .SelectMany(group => group.Projects.OrderBy(project => project.SortOrder))
            .Select(project => (Guid?)project.Project.ProjectId)
            .FirstOrDefault();
    }

    private sealed record GroupDefinition(
        string GroupKey,
        string DisplayName,
        ProjectStatus Status,
        int SortOrder);
}
