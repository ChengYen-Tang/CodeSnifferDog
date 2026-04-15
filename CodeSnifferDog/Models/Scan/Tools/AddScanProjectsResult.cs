namespace CodeSnifferDog.Models.Scan.Tools;

public sealed class AddScanProjectsResult
{
    public required IReadOnlyList<string> ScanProjectIds { get; init; }
}
