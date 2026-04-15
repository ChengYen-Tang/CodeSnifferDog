namespace CodeSnifferDog.Models.Review;

public sealed class SubmitReviewVerdictArgs
{
    public required bool Approved { get; init; }

    public required string Message { get; init; }
}
