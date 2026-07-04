namespace CodeSnifferDog.Models.Common.Tools;

/// <summary>
/// Arguments used to invoke the shell command tool.
/// </summary>
public sealed class RunShellCommandArgs
{
    /// <summary>
    /// Gets the shell command line to execute.
    /// </summary>
    public required string Command { get; init; }
}
