namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal interface ISnapshotQueryService
{
    Task<SnapshotReadModel?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default);

    Task<HistorySnapshotReadModel?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
