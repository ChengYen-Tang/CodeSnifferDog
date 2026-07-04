namespace CodeSnifferDog.Models.Review;

/// <summary>
/// Arguments used to submit a review verdict.
/// </summary>
public sealed class SubmitReviewVerdictArgs
{
    /// <summary>
    /// Gets whether the reviewed output is approved.
    /// </summary>
    public required bool Approved { get; init; }

    /// <summary>
    /// Gets the explanation attached to the verdict.
    /// </summary>
    public required string Message { get; init; }
}
