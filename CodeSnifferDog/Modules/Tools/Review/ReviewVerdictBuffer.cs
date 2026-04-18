using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Modules.Tools.Review;

public sealed class ReviewVerdictBuffer
{
    private const string DefaultScopeKey = "__default__";
    private readonly Dictionary<string, ReviewVerdict> _latestByScope = [];
    private readonly Lock _syncRoot = new();

    public ReviewVerdict? Latest => GetLatest(DefaultScopeKey);

    public ReviewVerdict? GetLatest(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
            return _latestByScope.GetValueOrDefault(scopeKey.Trim());
    }

    public void Reset() => Reset(DefaultScopeKey);

    public void Reset(string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        lock (_syncRoot)
            _latestByScope.Remove(scopeKey.Trim());
    }

    public void Submit(bool approved, string message) => Submit(DefaultScopeKey, approved, message);

    public void Submit(string scopeKey, bool approved, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        lock (_syncRoot)
            _latestByScope[scopeKey.Trim()] = new ReviewVerdict
            {
                Approved = approved,
                Message = message,
            };
    }
}
