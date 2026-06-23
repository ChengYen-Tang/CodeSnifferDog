using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Modules.Tools.Review;

internal sealed class ReviewVerdictToolService(ReviewVerdictBuffer verdictBuffer)
{
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;

    public ValueTask<bool> SubmitReviewVerdictAsync(SubmitReviewVerdictArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }
}
