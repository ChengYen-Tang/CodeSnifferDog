namespace CodeSnifferDog.Server.Services.Projects.Queries;

internal interface IProjectSidebarQueryService
{
    Task<ProjectSidebarSnapshotReadModel> GetSnapshotAsync(
        Guid? selectedProjectId,
        CancellationToken cancellationToken = default);
}
