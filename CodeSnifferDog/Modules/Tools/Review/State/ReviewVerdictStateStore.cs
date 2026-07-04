using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Modules.Tools.Review.State;

/// <summary>
/// Stores the latest review verdict for each verdict scope.
/// </summary>
internal sealed class ReviewVerdictStateStore
{
    private readonly Dictionary<string, ReviewVerdict> _latestByScope = [];

    /// <summary>
    /// Gets the latest verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <returns>The latest verdict, or <see langword="null"/> when none exists.</returns>
    public ReviewVerdict? GetLatest(string scopeKey) =>
        _latestByScope.GetValueOrDefault(scopeKey.Trim());

    /// <summary>
    /// Removes the latest verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    public void Reset(string scopeKey) =>
        _latestByScope.Remove(scopeKey.Trim());

    /// <summary>
    /// Stores the latest verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <param name="approved">Whether the verdict approved the work.</param>
    /// <param name="message">Verdict message.</param>
    public void Submit(string scopeKey, bool approved, string message) =>
        _latestByScope[scopeKey.Trim()] = new ReviewVerdict
        {
            Approved = approved,
            Message = message,
        };

    /// <summary>
    /// Clones the latest verdict for one scope.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <returns>The cloned verdict, or <see langword="null"/> when none exists.</returns>
    public ReviewVerdict? Clone(string scopeKey)
    {
        if (!_latestByScope.TryGetValue(scopeKey.Trim(), out ReviewVerdict? verdict))
            return null;

        return CloneVerdict(verdict);
    }

    /// <summary>
    /// Restores one scope from a verdict snapshot.
    /// </summary>
    /// <param name="scopeKey">Verdict scope key.</param>
    /// <param name="snapshot">Snapshot to restore.</param>
    public void Restore(string scopeKey, ReviewVerdict? snapshot)
    {
        string normalizedScopeKey = scopeKey.Trim();

        if (snapshot is null)
            _latestByScope.Remove(normalizedScopeKey);
        else
            _latestByScope[normalizedScopeKey] = CloneVerdict(snapshot);
    }

    /// <summary>
    /// Clones one verdict.
    /// </summary>
    /// <param name="verdict">Verdict to clone.</param>
    /// <returns>The cloned verdict.</returns>
    private static ReviewVerdict CloneVerdict(ReviewVerdict verdict) =>
        new()
        {
            Approved = verdict.Approved,
            Message = verdict.Message,
        };
}
