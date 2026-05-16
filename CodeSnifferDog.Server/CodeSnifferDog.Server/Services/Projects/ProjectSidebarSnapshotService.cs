using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.Projects;

public sealed class ProjectSidebarSnapshotService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory) : IProjectSidebarSnapshotService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<ProjectSidebarSnapshotDto> GetSnapshotAsync(Guid? selectedProjectId, CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ProjectSidebarProjectRow> projects = await dbContext.Projects
            .AsNoTracking()
            .Select(project => new ProjectSidebarProjectRow(
                project.Id,
                project.OriginalFileName,
                project.Status,
                project.CreatedAtUtc,
                project.QueueTimestampUtc,
                project.FinishedAtUtc,
                project.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProjectSidebarGroupDto> groups = CreateGroupDefinitions()
            .Select(group => new ProjectSidebarGroupDto
            {
                GroupKey = group.GroupKey,
                DisplayName = group.DisplayName,
                Status = group.Status,
                SortOrder = group.SortOrder,
                Projects = projects
                    .Where(project => MapStatus(project.Status) == group.Status)
                    .OrderBy(project => GetProjectSortPrimary(project))
                    .ThenBy(project => project.CreatedAtUtc)
                    .ThenBy(project => project.ProjectId)
                    .Select((project, index) => new ProjectSidebarProjectDto
                    {
                        ProjectId = project.ProjectId,
                        OriginalFileName = project.OriginalFileName,
                        Status = MapStatus(project.Status),
                        CreatedAtUtc = project.CreatedAtUtc,
                        SortOrder = index,
                    })
                    .ToList(),
            })
            .OrderBy(group => group.SortOrder)
            .ToList();

        Guid? resolvedSelectedProjectId = ResolveSelectedProjectId(selectedProjectId, groups);

        return new ProjectSidebarSnapshotDto
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SelectedProjectId = resolvedSelectedProjectId,
            Groups = groups,
        };
    }

    private static IReadOnlyList<ProjectSidebarGroupDefinition> CreateGroupDefinitions() =>
    [
        new("reviewing", "Reviewing", ProjectStatus.Reviewing, 0),
        new("completed", "Completed", ProjectStatus.Completed, 1),
        new("queued", "Queued", ProjectStatus.Queued, 2),
        new("failed", "Failed", ProjectStatus.Failed, 3),
        new("canceled", "Canceled", ProjectStatus.Canceled, 4),
    ];

    private static DateTimeOffset GetProjectSortPrimary(ProjectSidebarProjectRow project) =>
        project.Status switch
        {
            ProjectProcessingStatus.Queued or ProjectProcessingStatus.Reviewing =>
                project.QueueTimestampUtc,
            _ => project.FinishedAtUtc ?? project.UpdatedAtUtc,
        };

    private static ProjectStatus MapStatus(ProjectProcessingStatus status) => status switch
    {
        ProjectProcessingStatus.Queued => ProjectStatus.Queued,
        ProjectProcessingStatus.Reviewing => ProjectStatus.Reviewing,
        ProjectProcessingStatus.Completed => ProjectStatus.Completed,
        ProjectProcessingStatus.Failed => ProjectStatus.Failed,
        ProjectProcessingStatus.Canceled => ProjectStatus.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported project status."),
    };

    private static Guid? ResolveSelectedProjectId(Guid? requestedSelectedProjectId, IReadOnlyList<ProjectSidebarGroupDto> groups)
    {
        HashSet<Guid> projectIds = groups
            .SelectMany(group => group.Projects)
            .Select(project => project.ProjectId)
            .ToHashSet();

        if (requestedSelectedProjectId is Guid selectedProjectId && projectIds.Contains(selectedProjectId))
            return selectedProjectId;

        return groups
            .OrderBy(group => group.SortOrder)
            .SelectMany(group => group.Projects.OrderBy(project => project.SortOrder))
            .Select(project => (Guid?)project.ProjectId)
            .FirstOrDefault();
    }

    private sealed record ProjectSidebarGroupDefinition(
        string GroupKey,
        string DisplayName,
        ProjectStatus Status,
        int SortOrder);

    private sealed record ProjectSidebarProjectRow(
        Guid ProjectId,
        string OriginalFileName,
        ProjectProcessingStatus Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset QueueTimestampUtc,
        DateTimeOffset? FinishedAtUtc,
        DateTimeOffset UpdatedAtUtc);
}
