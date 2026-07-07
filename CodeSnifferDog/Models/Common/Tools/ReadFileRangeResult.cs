namespace CodeSnifferDog.Models.Common.Tools;

/// <summary>
/// Captures the result of reading a bounded file line range.
/// </summary>
public sealed class ReadFileRangeResult
{
    /// <summary>
    /// Gets whether the requested range was returned.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the resolved file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the requested one-based first line.
    /// </summary>
    public required int OffsetLine { get; init; }

    /// <summary>
    /// Gets the requested maximum line count.
    /// </summary>
    public required int LimitLines { get; init; }

    /// <summary>
    /// Gets the one-based first returned line, or zero when no content was returned.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Gets the one-based last returned line, or zero when no content was returned.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Gets the total number of lines in the file when known, or zero when the reader stopped before EOF.
    /// </summary>
    public required int TotalLines { get; init; }

    /// <summary>
    /// Gets the original file size in bytes when known.
    /// </summary>
    public required long OriginalBytes { get; init; }

    /// <summary>
    /// Gets the returned file content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets an informational or error message for the caller.
    /// </summary>
    public required string Message { get; init; }
}
