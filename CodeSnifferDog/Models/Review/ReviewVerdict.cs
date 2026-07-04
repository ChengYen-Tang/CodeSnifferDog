namespace CodeSnifferDog.Models.Review;

/// <summary>
/// Represents a reviewer decision together with its explanatory message.
/// </summary>
public sealed class ReviewVerdict
{
    /// <summary>
    /// Gets whether the reviewed output was approved.
    /// </summary>
    public required bool Approved { get; init; }

    /// <summary>
    /// Gets the explanation attached to the verdict.
    /// </summary>
    public required string Message { get; init; }
}
