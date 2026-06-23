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

    public ValueTask<bool> SubmitReviewVerdictAsync(string scopeKey, SubmitReviewVerdictArgs args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(scopeKey.Trim(), args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }
}
