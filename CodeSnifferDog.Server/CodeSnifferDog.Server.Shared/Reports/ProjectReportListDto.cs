namespace CodeSnifferDog.Server.Shared.Reports;

public sealed class ProjectReportListDto
{
    public required string OriginalFileName { get; init; }

    public required IReadOnlyList<ProjectReportListItemDto> Reports { get; init; }
}
