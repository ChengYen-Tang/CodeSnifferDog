namespace CodeSnifferDog.Server.Shared.Reports.Project;

public sealed class ContentDto
{
    public required Guid ReportId { get; init; }

    public required string RuleName { get; init; }

    public required string MarkdownContent { get; init; }
}
