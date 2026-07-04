namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Persists an agent emitted during project execution.
/// </summary>
public sealed class ProjectAgentRecord
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning agent group identifier.
    /// </summary>
    public Guid ProjectAgentGroupId { get; set; }

    /// <summary>
    /// Gets or sets the runtime key emitted by the execution runtime.
    /// </summary>
    public required string RuntimeKey { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to users.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the system prompt assigned to the agent.
    /// </summary>
    public required string SystemPrompt { get; set; }

    /// <summary>
    /// Gets or sets the current persisted agent status.
    /// </summary>
    public ProjectAgentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the agent was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning agent group navigation property.
    /// </summary>
    public ProjectAgentGroupRecord? Group { get; set; }

    /// <summary>
    /// Gets or sets the persisted timeline entries for the agent.
    /// </summary>
    public List<ProjectAgentTimelineEntryRecord> TimelineEntries { get; set; } = [];
}
