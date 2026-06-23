using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Common;

internal static class CommonToolFactory
{
    public static IList<AITool> CreateTools(CommonToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.RunShellCommandTool,
            "RunShellCommand",
            "Run one shell command in the repository root path. Use PowerShell on Windows and bash on Linux/macOS. Pass only the command text to execute.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.RunRipgrepCommandTool,
            "RunRipgrepCommand",
            "Run one ripgrep search command in the repository root path. Pass only the arguments after rg. Do not include rg in the command text. Example: use \"-n \\\"SystemPrompt\\\" .\" instead of \"rg -n \\\"SystemPrompt\\\" .\".",
            serializerOptions: null),
    ];
}

internal readonly record struct CommonToolCallbacks(
    RunShellCommandToolCallback RunShellCommandTool,
    RunRipgrepCommandToolCallback RunRipgrepCommandTool);

internal delegate ValueTask<CommandExecutionResult> RunShellCommandToolCallback(
    string Command,
    CancellationToken cancellationToken);

internal delegate ValueTask<CommandExecutionResult> RunRipgrepCommandToolCallback(
    string Command,
    CancellationToken cancellationToken);
