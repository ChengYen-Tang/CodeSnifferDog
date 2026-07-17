namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Stores the current workflow attempt identifier in async-local state so downstream code can correlate work with one attempt.
/// </summary>
internal static class AgentRunAttemptContext
{
    private static readonly AsyncLocal<AttemptContext?> CurrentAttempt = new();

    /// <summary>
    /// Gets the current workflow attempt identifier, when execution is inside <see cref="RunAsync{T}(Guid, Func{Task{T}})" />.
    /// </summary>
    public static Guid? CurrentAttemptId => CurrentAttempt.Value?.AttemptId;
    public static string? CurrentAgentGroupKey => CurrentAttempt.Value?.AgentGroupKey;
    public static string? CurrentAgentKey => CurrentAttempt.Value?.AgentKey;

    /// <summary>Allocates a monotonically increasing model-call identifier within the current attempt.</summary>
    public static int? GetNextModelCallNumber() =>
        CurrentAttempt.Value is { } attempt ? Interlocked.Increment(ref attempt.ModelCallCount) : null;

    /// <summary>Gets attempt-lifetime state, or <see langword="null"/> outside an agent-run attempt.</summary>
    public static T? GetOrCreateAttemptState<T>(object key, Func<T> create) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(create);
        AttemptContext? attempt = CurrentAttempt.Value;
        return attempt?.GetOrCreateState(key, create);
    }

    /// <summary>
    /// Executes a callback while exposing one attempt identifier through <see cref="CurrentAttemptId" />.
    /// </summary>
    /// <typeparam name="T">Type returned by the callback.</typeparam>
    /// <param name="attemptId">Attempt identifier to expose for the current async flow.</param>
    /// <param name="callback">Callback executed within the attempt context.</param>
    /// <returns>The callback result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback" /> is <see langword="null" />.</exception>
    public static async Task<T> RunAsync<T>(Guid attemptId, Func<Task<T>> callback)
        => await RunAsync(attemptId, null, null, callback).ConfigureAwait(false);

    /// <summary>Executes a callback while exposing attempt and agent identity to downstream telemetry.</summary>
    public static async Task<T> RunAsync<T>(Guid attemptId, string? agentGroupKey, string? agentKey, Func<Task<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        AttemptContext? previousAttempt = CurrentAttempt.Value;
        CurrentAttempt.Value = new AttemptContext(attemptId, agentGroupKey, agentKey);

        try
        {
            return await callback().ConfigureAwait(false);
        }
        finally
        {
            CurrentAttempt.Value = previousAttempt;
        }
    }

    private sealed class AttemptContext(Guid attemptId, string? agentGroupKey, string? agentKey)
    {
        private readonly object _stateLock = new();
        private readonly Dictionary<object, object> _state = [];
        public Guid AttemptId { get; } = attemptId;
        public string? AgentGroupKey { get; } = agentGroupKey;
        public string? AgentKey { get; } = agentKey;
        public int ModelCallCount;

        public T GetOrCreateState<T>(object key, Func<T> create) where T : class
        {
            lock (_stateLock)
            {
                if (_state.TryGetValue(key, out object? existing))
                    return (T)existing;
                T created = create();
                _state.Add(key, created);
                return created;
            }
        }
    }
}
