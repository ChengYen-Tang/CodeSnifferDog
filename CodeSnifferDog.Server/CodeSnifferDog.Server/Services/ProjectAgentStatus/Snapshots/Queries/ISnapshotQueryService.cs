namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

/// <summary>
/// Loads persisted read models required to build project-agent status snapshots.
/// </summary>
internal interface ISnapshotQueryService
{
    /// <summary>
    /// Loads the current status snapshot read model for one project.
    /// </summary>
    /// <param name="projectId">Project identifier to load.</param>
    /// <param name="selectedAgentId">Optional selected agent whose history should be included.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The snapshot read model, or <see langword="null" /> when the project is absent.</returns>
    Task<SnapshotReadModel?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the persisted history read model for one agent in one project.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the agent.</param>
    /// <param name="agentId">Agent identifier whose history should be loaded.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The history read model, or <see langword="null" /> when the agent does not belong to the project.</returns>
    Task<HistorySnapshotReadModel?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
