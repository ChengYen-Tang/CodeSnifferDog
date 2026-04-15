namespace CodeSnifferDog.Models.Common.Tools;

public sealed class CommandExecutionResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }
}
