namespace CodeSnifferDog.Models.Common.Tools;

/// <summary>
/// Arguments used to read a bounded file line range.
/// </summary>
public sealed class ReadFileRangeArgs
{
    /// <summary>
    /// Gets the repository-relative or absolute file path to read.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the one-based first line to read.
    /// </summary>
    public required int OffsetLine { get; init; }

    /// <summary>
    /// Gets the maximum number of lines to read.
    /// </summary>
    public required int LimitLines { get; init; }
}
