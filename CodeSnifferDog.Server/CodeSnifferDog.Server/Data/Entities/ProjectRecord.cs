namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Persists project upload and execution state.
/// </summary>
public sealed class ProjectRecord
{
    /// <summary>
    /// Gets or sets the project identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the original uploaded file name.
    /// </summary>
    public required string OriginalFileName { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the stored upload archive.
    /// </summary>
    public required string StoredZipRelativePath { get; set; }

    /// <summary>
    /// Gets or sets the current processing status.
    /// </summary>
    public ProjectProcessingStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the uploaded file size, in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets when the project record was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the project record was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the project entered the execution queue.
    /// </summary>
    public DateTimeOffset QueueTimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets when execution started.
    /// </summary>
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when execution finished.
    /// </summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the failure reason when processing did not complete successfully.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the persisted rule reports for the project.
    /// </summary>
    public List<ProjectRuleReportRecord> RuleReports { get; set; } = [];

    /// <summary>
    /// Gets or sets the persisted agent groups for the project.
    /// </summary>
    public List<ProjectAgentGroupRecord> AgentGroups { get; set; } = [];
}
