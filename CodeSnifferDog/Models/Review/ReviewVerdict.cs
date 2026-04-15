namespace CodeSnifferDog.Models.Review;

public sealed class ReviewVerdict
{
    public required bool Approved { get; init; }

    public required string Message { get; init; }
}
