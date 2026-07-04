using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Builds the tool set that exposes shell and ripgrep commands.
/// </summary>
public sealed class CommonToolSet
{
    private readonly CommonCommandToolService _commandToolService;

    public CommonToolSet(string repositoryRootPath, ILoggerFactory? loggerFactory = null)
        : this(new CommonCommandToolService(
            repositoryRootPath,
            loggerFactory?.CreateLogger<CommonCommandToolService>()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonToolSet"/> class for tests or composed services.
    /// </summary>
    /// <param name="commandToolService">Service that executes shell and ripgrep commands.</param>
    internal CommonToolSet(CommonCommandToolService commandToolService)
    {
        _commandToolService = commandToolService;
    }

    /// <summary>
    /// Creates the common AI tools.
    /// </summary>
    /// <returns>The created tools.</returns>
    public IList<AITool> CreateTools()
        =>
        CommonToolFactory.CreateTools(new CommonToolCallbacks(
            RunShellCommandToolAsync,
            RunRipgrepCommandToolAsync));

    [Description("Run one shell command in the repository root path. Use PowerShell on Windows and bash on Linux or macOS.")]
    private ValueTask<CommandExecutionResult> RunShellCommandToolAsync(
        [Description("The shell command text to execute inside the repository root path.")]
        string Command,
        CancellationToken cancellationToken) =>
        RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = Command,
            },
            cancellationToken);

    [Description("Run one ripgrep search command in the repository root path.")]
    private ValueTask<CommandExecutionResult> RunRipgrepCommandToolAsync(
        [Description("Arguments after rg. Do not include rg or rg.exe. Example: use \"-n \\\"SystemPrompt\\\" .\" instead of \"rg -n \\\"SystemPrompt\\\" .\". Full paths are allowed when you need to inspect files outside the repository root path.")]
        string Command,
        CancellationToken cancellationToken) =>
        RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = Command,
            },
            cancellationToken);

    /// <summary>
    /// Runs one shell command.
    /// </summary>
    /// <param name="args">Shell command arguments.</param>
    /// <param name="cancellationToken">Token that cancels command execution.</param>
    /// <returns>The command execution result.</returns>
    public ValueTask<CommandExecutionResult> RunShellCommandAsync(
        RunShellCommandArgs args,
        CancellationToken cancellationToken) =>
        _commandToolService.RunShellCommandAsync(args, cancellationToken);

    /// <summary>
    /// Runs one ripgrep command.
    /// </summary>
    /// <param name="args">Ripgrep command arguments.</param>
    /// <param name="cancellationToken">Token that cancels command execution.</param>
    /// <returns>The command execution result.</returns>
    public ValueTask<CommandExecutionResult> RunRipgrepCommandAsync(
        RunRipgrepCommandArgs args,
        CancellationToken cancellationToken) =>
        _commandToolService.RunRipgrepCommandAsync(args, cancellationToken);
}
