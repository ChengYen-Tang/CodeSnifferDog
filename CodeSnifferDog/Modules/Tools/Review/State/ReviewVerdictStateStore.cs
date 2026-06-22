using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Modules.Tools.Review.State;

internal sealed class ReviewVerdictStateStore
{
    private readonly Dictionary<string, ReviewVerdict> _latestByScope = [];

    public ReviewVerdict? GetLatest(string scopeKey) =>
        _latestByScope.GetValueOrDefault(scopeKey.Trim());

    public void Reset(string scopeKey) =>
        _latestByScope.Remove(scopeKey.Trim());

    public void Submit(string scopeKey, bool approved, string message) =>
        _latestByScope[scopeKey.Trim()] = new ReviewVerdict
        {
            Approved = approved,
            Message = message,
        };

    public ReviewVerdict? Clone(string scopeKey)
    {
        if (!_latestByScope.TryGetValue(scopeKey.Trim(), out ReviewVerdict? verdict))
            return null;

        return CloneVerdict(verdict);
    }

    public void Restore(string scopeKey, ReviewVerdict? snapshot)
    {
        string normalizedScopeKey = scopeKey.Trim();

        if (snapshot is null)
            _latestByScope.Remove(normalizedScopeKey);
        else
            _latestByScope[normalizedScopeKey] = CloneVerdict(snapshot);
    }

    private static ReviewVerdict CloneVerdict(ReviewVerdict verdict) =>
        new()
        {
            Approved = verdict.Approved,
            Message = verdict.Message,
        };
}
