namespace CodeSnifferDog.Server.Shared.Reports.Project;

public sealed class BundleDto
{
    public required string OriginalFileName { get; init; }

    public required IReadOnlyList<RuleDto> Reports { get; init; }
}
