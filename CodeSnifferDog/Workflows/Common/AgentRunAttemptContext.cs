namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Stores the current workflow attempt identifier in async-local state so downstream code can correlate work with one attempt.
/// </summary>
internal static class AgentRunAttemptContext
{
    private static readonly AsyncLocal<Guid?> CurrentAttempt = new();

    /// <summary>
    /// Gets the current workflow attempt identifier, when execution is inside <see cref="RunAsync{T}(Guid, Func{Task{T}})" />.
    /// </summary>
    public static Guid? CurrentAttemptId => CurrentAttempt.Value;

    /// <summary>
    /// Executes a callback while exposing one attempt identifier through <see cref="CurrentAttemptId" />.
    /// </summary>
    /// <typeparam name="T">Type returned by the callback.</typeparam>
    /// <param name="attemptId">Attempt identifier to expose for the current async flow.</param>
    /// <param name="callback">Callback executed within the attempt context.</param>
    /// <returns>The callback result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback" /> is <see langword="null" />.</exception>
    public static async Task<T> RunAsync<T>(Guid attemptId, Func<Task<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Guid? previousAttemptId = CurrentAttempt.Value;
        CurrentAttempt.Value = attemptId;

        try
        {
            return await callback().ConfigureAwait(false);
        }
        finally
        {
            CurrentAttempt.Value = previousAttemptId;
        }
    }
}
