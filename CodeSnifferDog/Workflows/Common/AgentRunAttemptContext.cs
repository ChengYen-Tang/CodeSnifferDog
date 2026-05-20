namespace CodeSnifferDog.Workflows.Common;

internal static class AgentRunAttemptContext
{
    private static readonly AsyncLocal<Guid?> CurrentAttempt = new();

    public static Guid? CurrentAttemptId => CurrentAttempt.Value;

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
