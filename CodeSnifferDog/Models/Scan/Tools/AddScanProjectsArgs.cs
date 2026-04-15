namespace CodeSnifferDog.Models.Scan.Tools;

public sealed class AddScanProjectsArgs
{
    public required IReadOnlyList<AddScanProjectArgs> Projects { get; init; }
}
