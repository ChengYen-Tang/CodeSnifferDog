namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Validates and evaluates configured retry limits.
/// </summary>
internal static class RetryLimit
{
    /// <summary>
    /// Determines whether the current attempt count has reached the configured limit.
    /// </summary>
    /// <param name="attempts">Current attempt count.</param>
    /// <param name="maxAttempts">Configured retry limit. Zero disables the limit.</param>
    /// <returns><see langword="true" /> when the limit is enabled and <paramref name="attempts" /> has reached it.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAttempts" /> is negative.</exception>
    public static bool IsReached(int attempts, int maxAttempts)
    {
        ThrowIfInvalid(maxAttempts);
        return maxAttempts > 0 && attempts >= maxAttempts;
    }

    /// <summary>
    /// Determines whether the current attempt count has exceeded the configured limit.
    /// </summary>
    /// <param name="attempts">Current attempt count.</param>
    /// <param name="maxAttempts">Configured retry limit. Zero disables the limit.</param>
    /// <returns><see langword="true" /> when the limit is enabled and <paramref name="attempts" /> is greater than it.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAttempts" /> is negative.</exception>
    public static bool IsExceeded(int attempts, int maxAttempts)
    {
        ThrowIfInvalid(maxAttempts);
        return maxAttempts > 0 && attempts > maxAttempts;
    }

    /// <summary>
    /// Validates that one retry limit can be evaluated safely.
    /// </summary>
    /// <param name="maxAttempts">Configured retry limit. Zero disables the limit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAttempts" /> is negative.</exception>
    public static void ThrowIfInvalid(int maxAttempts)
    {
        if (maxAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Retry limit must be zero or greater.");
    }
}
