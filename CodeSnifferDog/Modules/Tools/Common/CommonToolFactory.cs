using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Creates the AI tools that expose shell, ripgrep, and ranged file access.
/// </summary>
internal static class CommonToolFactory
{
    /// <summary>
    /// Creates the common command tools.
    /// </summary>
    /// <param name="callbacks">Callbacks invoked by the created tools.</param>
    /// <returns>The created tools.</returns>
    public static IList<AITool> CreateTools(CommonToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.ReadFileRangeTool,
            "ReadFileRange",
            "Read a bounded line range from one file. Use ReadFileRange, not Shell, for file content. For large files, read smaller ranges with offsetLine/limitLines. The tool name is ReadFileRange, not ReadFile.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.RunRipgrepCommandTool,
            "Ripgrep",
            "Run one ripgrep search command in the repository root path. Pass only the arguments after rg. Do not include rg in the command text. Example: use \"-n \\\"SystemPrompt\\\" .\" instead of \"rg -n \\\"SystemPrompt\\\" .\".",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.RunShellCommandTool,
            "Shell",
            "Run one PowerShell 7 command in the repository root path. Shell is for narrow, foreground operational commands only. Do not start background or detached work: cancellation stops the hosted PowerShell pipeline but cannot guarantee cleanup of child processes spawned by native commands. Do not use Shell to read file content, run unbounded recursive directory listings such as Get-ChildItem -Recurse, or produce large output. Use Ripgrep to search or list files narrowly, and use ReadFileRange to read files.",
            serializerOptions: null),
    ];
}

/// <summary>
/// Groups callbacks used by the common command tools.
/// </summary>
/// <param name="RunShellCommandTool">Callback for running shell commands.</param>
/// <param name="RunRipgrepCommandTool">Callback for running ripgrep commands.</param>
/// <param name="ReadFileRangeTool">Callback for reading bounded file ranges.</param>
internal readonly record struct CommonToolCallbacks(
    RunShellCommandToolCallback RunShellCommandTool,
    RunRipgrepCommandToolCallback RunRipgrepCommandTool,
    ReadFileRangeToolCallback ReadFileRangeTool);

/// <summary>
/// Represents the callback used to run one shell command.
/// </summary>
internal delegate ValueTask<CommandExecutionResult> RunShellCommandToolCallback(
    string Command,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to run one ripgrep command.
/// </summary>
internal delegate ValueTask<CommandExecutionResult> RunRipgrepCommandToolCallback(
    string Command,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to read a bounded file range.
/// </summary>
internal delegate ValueTask<CommandExecutionResult> ReadFileRangeToolCallback(
    string Path,
    int OffsetLine,
    int LimitLines,
    CancellationToken cancellationToken);
