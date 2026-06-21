namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal interface IProjectAgentStatusSnapshotQueryService
{
    Task<ProjectAgentStatusSnapshotReadModel?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectAgentHistorySnapshotReadModel?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
