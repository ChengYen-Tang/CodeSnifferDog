namespace CodeSnifferDog.Models.Scan;

public sealed class StoredScanProject
{
    public required string ScanProjectId { get; init; }

    public required string ProjectName { get; init; }

    public required string ProjectPath { get; init; }

    public required string ProjectType { get; init; }

    public required string Reason { get; init; }
}
