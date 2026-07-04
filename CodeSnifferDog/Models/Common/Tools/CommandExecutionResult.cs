namespace CodeSnifferDog.Models.Common.Tools;

/// <summary>
/// Captures the output streams and exit code from a command execution.
/// </summary>
public sealed class CommandExecutionResult
{
    /// <summary>
    /// Gets the process exit code.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets the captured standard-output text.
    /// </summary>
    public required string StandardOutput { get; init; }

    /// <summary>
    /// Gets the captured standard-error text.
    /// </summary>
    public required string StandardError { get; init; }
}
