namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Persists an agent group emitted during project execution.
/// </summary>
public sealed class ProjectAgentGroupRecord
{
    /// <summary>
    /// Gets or sets the agent group identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the runtime key emitted by the execution runtime.
    /// </summary>
    public required string RuntimeKey { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to users.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets when the agent group was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning project navigation property.
    /// </summary>
    public ProjectRecord? Project { get; set; }

    /// <summary>
    /// Gets or sets the persisted agents that belong to the group.
    /// </summary>
    public List<ProjectAgentRecord> Agents { get; set; } = [];
}
