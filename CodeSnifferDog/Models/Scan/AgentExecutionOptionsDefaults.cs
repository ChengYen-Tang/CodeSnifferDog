namespace CodeSnifferDog.Models.Scan;

/// <summary>
/// Provides shared default execution limits for scan-derived workflows.
/// </summary>
internal static class AgentExecutionOptionsDefaults
{
    /// <summary>
    /// Default maximum number of consecutive run failures allowed before giving up.
    /// </summary>
    public const int MaxConsecutiveRunFailures = 5;

    /// <summary>
    /// Default timeout allowed for one agent run.
    /// </summary>
    public static readonly TimeSpan AgentRunTimeout = TimeSpan.FromMinutes(5);
}
