using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Modules.Tools.Review;

/// <summary>
/// Stores verifier verdict submissions in a <see cref="ReviewVerdictBuffer" />.
/// </summary>
internal sealed class ReviewVerdictToolService(ReviewVerdictBuffer verdictBuffer)
{
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;

    /// <summary>
    /// Stores a default-scope review verdict.
    /// </summary>
    /// <param name="args">Verdict arguments.</param>
    /// <returns><see langword="true"/> when the verdict was stored.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the verdict message is blank.</exception>
    public ValueTask<bool> SubmitReviewVerdictAsync(SubmitReviewVerdictArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Stores a scoped review verdict.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <param name="args">Verdict arguments.</param>
    /// <returns><see langword="true"/> when the verdict was stored.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scopeKey"/> or the verdict message is blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    public ValueTask<bool> SubmitReviewVerdictAsync(string scopeKey, SubmitReviewVerdictArgs args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(scopeKey.Trim(), args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }
}
