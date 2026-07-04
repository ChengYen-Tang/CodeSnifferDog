namespace CodeSnifferDog.Models.Common.Tools;

/// <summary>
/// Arguments used to invoke the ripgrep command tool.
/// </summary>
public sealed class RunRipgrepCommandArgs
{
    /// <summary>
    /// Gets the ripgrep command line to execute.
    /// </summary>
    public required string Command { get; init; }
}
