namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Stores the current workflow attempt identifier in async-local state so downstream code can correlate work with one attempt.
/// </summary>
internal static class AgentRunAttemptContext
{
    private static readonly AsyncLocal<AttemptContext?> CurrentAttempt = new();
    private static readonly AsyncLocal<LogicalRunContext?> CurrentLogicalRun = new();
    private static readonly AsyncLocal<PreCompactedContextScope?> CurrentPreCompactedContext = new();

    /// <summary>
    /// Gets the current workflow attempt identifier, when execution is inside <see cref="RunAsync{T}(Guid, Func{Task{T}})" />.
    /// </summary>
    public static Guid? CurrentAttemptId => CurrentAttempt.Value?.AttemptId;
    public static string? CurrentAgentGroupKey => CurrentAttempt.Value?.AgentGroupKey;
    public static string? CurrentAgentKey => CurrentAttempt.Value?.AgentKey;

    /// <summary>
    /// Gets whether the current model invocation already received a transcript prepared by the
    /// reactive-compaction runtime.
    /// </summary>
    public static bool IsPreCompactedContext => CurrentPreCompactedContext.Value is not null;

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
    /// Gets state that survives every retry attempt in the current logical agent run.
    /// </summary>
    /// <remarks>
    /// The state is intentionally unavailable outside <see cref="RunLogicalRunAsync{T}" /> so shared chat-client
    /// instances cannot leak usage or compaction state between unrelated workflow runs.
    /// </remarks>
    public static T? GetOrCreateLogicalRunState<T>(object key, Func<T> create) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(create);
        return CurrentLogicalRun.Value?.GetOrCreateState(key, create);
    }

    /// <summary>
    /// Gets named state for the current agent scope, or <see langword="null" /> outside a logical run.
    /// </summary>
    /// <param name="stateName">Stable name of the state owned by the caller.</param>
    /// <param name="create">Factory used to create the state on first access.</param>
    /// <returns>The state associated with the current logical agent scope.</returns>
    public static T? GetOrCreateLogicalRunState<T>(string stateName, Func<T> create) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(create);
        return CurrentLogicalRun.Value?.GetOrCreateNamedState(stateName, create);
    }

    /// <summary>
    /// Executes a callback within a state scope that survives all nested agent attempts and retries.
    /// </summary>
    /// <typeparam name="T">Type returned by the callback.</typeparam>
    /// <param name="callback">Callback executed within the logical agent-run scope.</param>
    /// <returns>The callback result.</returns>
    public static async Task<T> RunLogicalRunAsync<T>(Func<Task<T>> callback)
        => await RunLogicalRunAsync(null, null, callback).ConfigureAwait(false);

    /// <summary>
    /// Executes a callback within a logical run identified by one stable agent event scope.
    /// </summary>
    /// <typeparam name="T">Type returned by the callback.</typeparam>
    /// <param name="agentGroupKey">Stable key of the owning agent group.</param>
    /// <param name="agentKey">Stable key of the agent inside the group.</param>
    /// <param name="callback">Callback executed within the logical agent-run scope.</param>
    /// <returns>The callback result.</returns>
    public static async Task<T> RunLogicalRunAsync<T>(
        string? agentGroupKey,
        string? agentKey,
        Func<Task<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        LogicalRunContext? previousLogicalRun = CurrentLogicalRun.Value;
        CurrentLogicalRun.Value = new LogicalRunContext(agentGroupKey, agentKey);

        try
        {
            return await callback().ConfigureAwait(false);
        }
        finally
        {
            CurrentLogicalRun.Value = previousLogicalRun;
        }
    }

    /// <summary>
    /// Executes a callback with the guarantee that its transcript was already reactively compacted.
    /// </summary>
    /// <typeparam name="T">Type returned by the callback.</typeparam>
    /// <param name="callback">Callback executed within the pre-compacted context scope.</param>
    /// <returns>The callback result.</returns>
    public static async Task<T> RunWithPreCompactedContextAsync<T>(Func<Task<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        PreCompactedContextScope? previousScope = CurrentPreCompactedContext.Value;
        CurrentPreCompactedContext.Value = new PreCompactedContextScope();

        try
        {
            return await callback().ConfigureAwait(false);
        }
        finally
        {
            CurrentPreCompactedContext.Value = previousScope;
        }
    }

    /// <summary>
    /// Executes a callback with the guarantee that its transcript was already reactively compacted.
    /// </summary>
    /// <param name="callback">Callback executed within the pre-compacted context scope.</param>
    /// <returns>A task representing the callback.</returns>
    public static async Task RunWithPreCompactedContextAsync(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        PreCompactedContextScope? previousScope = CurrentPreCompactedContext.Value;
        CurrentPreCompactedContext.Value = new PreCompactedContextScope();

        try
        {
            await callback().ConfigureAwait(false);
        }
        finally
        {
            CurrentPreCompactedContext.Value = previousScope;
        }
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

    private sealed class LogicalRunContext(string? agentGroupKey, string? agentKey)
    {
        private readonly object _stateLock = new();
        private readonly Dictionary<object, object> _state = [];

        private string? AgentGroupKey { get; } = agentGroupKey;
        private string? AgentKey { get; } = agentKey;

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

        public T GetOrCreateNamedState<T>(string stateName, Func<T> create) where T : class =>
            GetOrCreateState(new NamedStateKey(stateName, AgentGroupKey, AgentKey), create);
    }

    private readonly record struct NamedStateKey(
        string StateName,
        string? AgentGroupKey,
        string? AgentKey);

    private sealed class PreCompactedContextScope;
}
