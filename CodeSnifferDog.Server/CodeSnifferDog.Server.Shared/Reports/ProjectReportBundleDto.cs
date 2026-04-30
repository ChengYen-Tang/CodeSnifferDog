namespace CodeSnifferDog.Server.Shared.Reports;

public sealed class ProjectReportBundleDto
{
    public required string OriginalFileName { get; init; }

    public required IReadOnlyList<ProjectRuleReportDto> Reports { get; init; }
}
