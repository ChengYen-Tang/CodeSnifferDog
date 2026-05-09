using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentSnapshots;

public interface IProjectAgentStatusSnapshotService
{
    Task<ProjectAgentStatusSnapshotDto?> GetSnapshotAsync(Guid projectId, CancellationToken cancellationToken = default);
}
