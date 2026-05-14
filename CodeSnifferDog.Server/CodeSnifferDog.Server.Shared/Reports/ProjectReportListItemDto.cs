namespace CodeSnifferDog.Server.Shared.Reports;

public sealed class ProjectReportListItemDto
{
    public required Guid ReportId { get; init; }

    public required string RuleName { get; init; }
}
