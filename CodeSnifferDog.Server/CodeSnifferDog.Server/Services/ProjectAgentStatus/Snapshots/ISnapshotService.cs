using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

public interface ISnapshotService
{
    Task<ProjectAgentStatusSnapshotDto?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectAgentHistorySnapshotDto?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
