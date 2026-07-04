using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Creates the AI tools that expose shell and ripgrep access.
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
            callbacks.RunShellCommandTool,
            "Shell",
            "Run one shell command in the repository root path. Use PowerShell on Windows and bash on Linux/macOS. Pass only the command text to execute.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.RunRipgrepCommandTool,
            "Ripgrep",
            "Run one ripgrep search command in the repository root path. Pass only the arguments after rg. Do not include rg in the command text. Example: use \"-n \\\"SystemPrompt\\\" .\" instead of \"rg -n \\\"SystemPrompt\\\" .\".",
            serializerOptions: null),
    ];
}

/// <summary>
/// Groups callbacks used by the common command tools.
/// </summary>
/// <param name="RunShellCommandTool">Callback for running shell commands.</param>
/// <param name="RunRipgrepCommandTool">Callback for running ripgrep commands.</param>
internal readonly record struct CommonToolCallbacks(
    RunShellCommandToolCallback RunShellCommandTool,
    RunRipgrepCommandToolCallback RunRipgrepCommandTool);

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
