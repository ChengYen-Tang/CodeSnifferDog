using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Builds the tool set that exposes bounded file reads, ripgrep search, and shell commands.
/// </summary>
public sealed class CommonToolSet
{
    private readonly CommonCommandToolService _commandToolService;
    private readonly CommonFileToolService _fileToolService;

    public CommonToolSet(string repositoryRootPath, ILoggerFactory? loggerFactory = null)
        : this(
            new CommonCommandToolService(
                repositoryRootPath,
                loggerFactory?.CreateLogger<CommonCommandToolService>()),
            new CommonFileToolService(repositoryRootPath))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonToolSet"/> class for tests or composed services.
    /// </summary>
    /// <param name="commandToolService">Service that executes shell and ripgrep commands.</param>
    internal CommonToolSet(CommonCommandToolService commandToolService)
        : this(commandToolService, new CommonFileToolService(commandToolService.RepositoryRootPath))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonToolSet"/> class for tests or composed services.
    /// </summary>
    /// <param name="commandToolService">Service that executes shell and ripgrep commands.</param>
    /// <param name="fileToolService">Service that reads bounded file ranges.</param>
    internal CommonToolSet(
        CommonCommandToolService commandToolService,
        CommonFileToolService fileToolService)
    {
        _commandToolService = commandToolService;
        _fileToolService = fileToolService;
    }

    /// <summary>
    /// Creates the common AI tools.
    /// </summary>
    /// <returns>The created tools.</returns>
    public IList<AITool> CreateTools()
        =>
        CommonToolFactory.CreateTools(new CommonToolCallbacks(
            RunShellCommandToolAsync,
            RunRipgrepCommandToolAsync,
            ReadFileRangeToolAsync));

    [Description("Run one shell command in the repository root path. Shell is for narrow operational commands only. Do not use Shell to read file content, run unbounded recursive directory listings such as Get-ChildItem -Recurse, or produce large output. Use Ripgrep to search or list files narrowly, and use ReadFileRange to read files.")]
    private ValueTask<CommandExecutionResult> RunShellCommandToolAsync(
        [Description("The shell command text to execute inside the repository root path. Keep output small. Do not use Get-Content, type, cat, or unbounded Get-ChildItem -Recurse output for file reading or discovery; use Ripgrep to narrow discovery and ReadFileRange to read files.")]
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

    [Description("Read a bounded line range from one file. Use ReadFileRange, not Shell, for file content. For large files, read smaller ranges with offsetLine/limitLines. The tool name is ReadFileRange, not ReadFile.")]
    private async ValueTask<CommandExecutionResult> ReadFileRangeToolAsync(
        [Description("The repository-relative or absolute file path to read.")]
        string Path,
        [Description("The one-based first line to read.")]
        int OffsetLine,
        [Description("The maximum number of lines to read. Use smaller values when the result is too large.")]
        int LimitLines,
        CancellationToken cancellationToken)
    {
        return await ReadFileRangeAsync(
            new ReadFileRangeArgs
            {
                Path = Path,
                OffsetLine = OffsetLine,
                LimitLines = LimitLines,
            },
            cancellationToken).ConfigureAwait(false);
    }

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

    /// <summary>
    /// Reads a bounded file line range.
    /// </summary>
    /// <param name="args">Range read arguments.</param>
    /// <param name="cancellationToken">Token that cancels file reading.</param>
    /// <returns>The range read result.</returns>
    public ValueTask<CommandExecutionResult> ReadFileRangeAsync(
        ReadFileRangeArgs args,
        CancellationToken cancellationToken) =>
        _fileToolService.ReadFileRangeAsync(args, cancellationToken);
}
