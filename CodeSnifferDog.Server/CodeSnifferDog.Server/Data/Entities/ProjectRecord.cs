namespace CodeSnifferDog.Server.Data.Entities;

public sealed class ProjectRecord
{
    public Guid Id { get; set; }

    public required string OriginalFileName { get; set; }

    public required string StoredZipRelativePath { get; set; }

    public ProjectProcessingStatus Status { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset QueueTimestampUtc { get; set; }

    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public List<ProjectRuleReportRecord> RuleReports { get; set; } = [];
}
