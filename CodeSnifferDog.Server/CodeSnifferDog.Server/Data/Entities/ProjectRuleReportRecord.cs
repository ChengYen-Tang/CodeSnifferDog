namespace CodeSnifferDog.Server.Data.Entities;

public sealed class ProjectRuleReportRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public required string RuleName { get; set; }

    public required string MarkdownContent { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ProjectRecord? Project { get; set; }
}
