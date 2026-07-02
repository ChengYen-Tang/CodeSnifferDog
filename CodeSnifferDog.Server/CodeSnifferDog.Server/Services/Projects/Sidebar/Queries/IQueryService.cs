namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;

internal interface IQueryService
{
    Task<SnapshotReadModel> GetSnapshotAsync(
        Guid? selectedProjectId,
        CancellationToken cancellationToken = default);
}
