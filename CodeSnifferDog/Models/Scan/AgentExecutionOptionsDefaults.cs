namespace CodeSnifferDog.Models.Scan;

internal static class AgentExecutionOptionsDefaults
{
    public const int MaxConsecutiveRunFailures = 5;
    public static readonly TimeSpan AgentRunTimeout = TimeSpan.FromMinutes(5);
}
