namespace CodeSnifferDog.Workflows.Common;

internal static class RetryLimit
{
    public static bool IsReached(int attempts, int maxAttempts)
    {
        ThrowIfInvalid(maxAttempts);
        return maxAttempts > 0 && attempts >= maxAttempts;
    }

    public static bool IsExceeded(int attempts, int maxAttempts)
    {
        ThrowIfInvalid(maxAttempts);
        return maxAttempts > 0 && attempts > maxAttempts;
    }

    public static void ThrowIfInvalid(int maxAttempts)
    {
        if (maxAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Retry limit must be zero or greater.");
    }
}
