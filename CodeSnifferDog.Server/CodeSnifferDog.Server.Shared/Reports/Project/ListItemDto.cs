namespace CodeSnifferDog.Server.Shared.Reports.Project;

public sealed class ListItemDto
{
    public required Guid ReportId { get; init; }

    public required string RuleName { get; init; }
}
