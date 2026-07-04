namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;

/// <summary>
/// Loads read models required to build the projects sidebar snapshot.
/// </summary>
internal interface IQueryService
{
    /// <summary>
    /// Loads the current sidebar snapshot read model.
    /// </summary>
    /// <param name="selectedProjectId">Optional selected project identifier from the client.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The sidebar snapshot read model.</returns>
    Task<SnapshotReadModel> GetSnapshotAsync(
        Guid? selectedProjectId,
        CancellationToken cancellationToken = default);
}
