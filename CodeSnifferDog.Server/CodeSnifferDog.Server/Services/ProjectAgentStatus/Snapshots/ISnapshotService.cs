using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

/// <summary>
/// Loads project-agent status snapshots and agent history snapshots.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Loads the current status snapshot for one project.
    /// </summary>
    /// <param name="projectId">Project identifier to load.</param>
    /// <param name="selectedAgentId">Optional selected agent whose history should be preloaded.</param>
    /// <param name="cancellationToken">Cancels snapshot loading.</param>
    /// <returns>The status snapshot, or <see langword="null" /> when the project is absent.</returns>
    Task<StatusSnapshotDto?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the full history snapshot for one agent in one project.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the agent.</param>
    /// <param name="agentId">Agent identifier whose history should be loaded.</param>
    /// <param name="cancellationToken">Cancels history loading.</param>
    /// <returns>The history snapshot, or <see langword="null" /> when the agent does not belong to the project.</returns>
    Task<HistorySnapshotDto?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
