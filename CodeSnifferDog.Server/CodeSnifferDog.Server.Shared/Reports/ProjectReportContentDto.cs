namespace CodeSnifferDog.Server.Shared.Reports;

public sealed class ProjectReportContentDto
{
    public required Guid ReportId { get; init; }

    public required string RuleName { get; init; }

    public required string MarkdownContent { get; init; }
}
