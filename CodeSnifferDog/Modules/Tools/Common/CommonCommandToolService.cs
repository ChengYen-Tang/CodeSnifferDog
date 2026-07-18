using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Output;
using CodeSnifferDog.Modules.Tools.Shell;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Executes shell and ripgrep commands for the common tool set.
/// </summary>
internal sealed class CommonCommandToolService
{
    private readonly string _repositoryRootPath;
    private readonly IShellCommandRunner _shellCommandRunner;
    private readonly CommandTextRunner _textRunner;
    private readonly RipgrepExecutablePathProvider _ripgrepExecutablePathProvider;
    private readonly ILogger<CommonCommandToolService>? _logger;

    /// <summary>
    /// Gets the normalized repository root path used as the command working directory.
    /// </summary>
    internal string RepositoryRootPath => _repositoryRootPath;

    public CommonCommandToolService(
        string repositoryRootPath,
        ILogger<CommonCommandToolService>? logger = null)
        : this(
            repositoryRootPath,
            new PowerShellCommandRunner(),
            CommandProcessRunner.RunAsync,
            new RipgrepAssetLocator().GetExecutablePath,
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonCommandToolService"/> class for tests or composed services.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path used as the working directory.</param>
    /// <param name="shellCommandRunner">In-process PowerShell runner used for shell commands.</param>
    /// <param name="textRunner">Runner used for raw-argument-string processes.</param>
    /// <param name="ripgrepExecutablePathProvider">Provider that resolves the ripgrep executable path.</param>
    /// <param name="logger">Optional logger.</param>
    internal CommonCommandToolService(
        string repositoryRootPath,
        IShellCommandRunner shellCommandRunner,
        CommandTextRunner textRunner,
        RipgrepExecutablePathProvider ripgrepExecutablePathProvider,
        ILogger<CommonCommandToolService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(shellCommandRunner);
        ArgumentNullException.ThrowIfNull(textRunner);
        ArgumentNullException.ThrowIfNull(ripgrepExecutablePathProvider);

        _repositoryRootPath = ValidateRepositoryRootPath(repositoryRootPath);
        _shellCommandRunner = shellCommandRunner;
        _textRunner = textRunner;
        _ripgrepExecutablePathProvider = ripgrepExecutablePathProvider;
        _logger = logger;
    }

    /// <summary>
    /// Runs one shell command in the repository root.
    /// </summary>
    /// <param name="args">Shell command arguments.</param>
    /// <param name="cancellationToken">Token that cancels command execution.</param>
    /// <returns>The command execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the command text is blank.</exception>
    public async ValueTask<CommandExecutionResult> RunShellCommandAsync(
        RunShellCommandArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Command);

        string command = args.Command.Trim();
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger?.LogDebug(
            "Shell tool started in {RepositoryRootPath}. Command: {Command}",
            _repositoryRootPath,
            command);

        CommandExecutionResult result = await _shellCommandRunner
            .RunAsync(command, _repositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        CommandExecutionResult limitedResult = CommandOutputLimiter.Limit(result);
        LogCommandResult("Shell", command, limitedResult, stopwatch.ElapsedMilliseconds);
        return limitedResult;
    }

    /// <summary>
    /// Runs one ripgrep command in the repository root.
    /// </summary>
    /// <param name="args">Ripgrep command arguments.</param>
    /// <param name="cancellationToken">Token that cancels command execution.</param>
    /// <returns>The command execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the command text is blank or includes the rg executable name.</exception>
    public async ValueTask<CommandExecutionResult> RunRipgrepCommandAsync(
        RunRipgrepCommandArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Command);

        string trimmedCommand = args.Command.Trim();

        if (StartsWithRipgrepExecutable(trimmedCommand))
            throw new ArgumentException("Command must not include the rg executable name.", nameof(args));

        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger?.LogDebug(
            "Ripgrep tool started in {RepositoryRootPath}. Arguments: {Command}",
            _repositoryRootPath,
            trimmedCommand);

        CommandExecutionResult result = await _textRunner(
            _ripgrepExecutablePathProvider(),
            trimmedCommand,
            _repositoryRootPath,
            cancellationToken).ConfigureAwait(false);

        CommandExecutionResult limitedResult = CommandOutputLimiter.Limit(result);
        LogCommandResult("Ripgrep", trimmedCommand, limitedResult, stopwatch.ElapsedMilliseconds);
        return limitedResult;
    }

    /// <summary>
    /// Logs the result of one executed tool command.
    /// </summary>
    /// <param name="toolName">Logical tool name.</param>
    /// <param name="command">Executed command text.</param>
    /// <param name="result">Execution result.</param>
    /// <param name="durationMs">Elapsed duration in milliseconds.</param>
    private void LogCommandResult(
        string toolName,
        string command,
        CommandExecutionResult result,
        long durationMs)
    {
        if (result.ExitCode == 0 || (toolName == "Ripgrep" && result.ExitCode == 1))
        {
            _logger?.LogDebug(
                "{ToolName} tool completed in {DurationMs} ms. Exit code: {ExitCode}; command: {Command}; stdout: {StandardOutput}; stderr: {StandardError}",
                toolName,
                durationMs,
                result.ExitCode,
                command,
                result.StandardOutput,
                result.StandardError);
            return;
        }

        _logger?.LogWarning(
            "{ToolName} tool completed with non-zero exit code {ExitCode} in {DurationMs} ms. Command: {Command}; stdout: {StandardOutput}; stderr: {StandardError}",
            toolName,
            result.ExitCode,
            durationMs,
            command,
            result.StandardOutput,
            result.StandardError);
    }

    /// <summary>
    /// Validates and normalizes the repository root path.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path to validate.</param>
    /// <returns>The normalized full path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="repositoryRootPath"/> is blank.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist.</exception>
    private static string ValidateRepositoryRootPath(string repositoryRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        string fullPath = Path.GetFullPath(repositoryRootPath.Trim());

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Repository root path does not exist: {fullPath}");

        return fullPath;
    }

    /// <summary>
    /// Determines whether a command string starts with the rg executable name.
    /// </summary>
    /// <param name="command">Command text to inspect.</param>
    /// <returns><see langword="true"/> when the command starts with rg; otherwise, <see langword="false"/>.</returns>
    internal static bool StartsWithRipgrepExecutable(string command)
    {
        ReadOnlySpan<char> remaining = command.AsSpan().TrimStart();

        if (remaining.IsEmpty)
            return false;

        if (!TryConsumeToken(ref remaining, "rg"))
            return false;

        TryConsumeToken(ref remaining, ".exe");
        return remaining.IsEmpty || char.IsWhiteSpace(remaining[0]);
    }

    /// <summary>
    /// Tries to consume one case-insensitive token from the start of a character span.
    /// </summary>
    /// <param name="remaining">Remaining character span.</param>
    /// <param name="token">Token to consume.</param>
    /// <returns><see langword="true"/> when the token was consumed; otherwise, <see langword="false"/>.</returns>
    private static bool TryConsumeToken(ref ReadOnlySpan<char> remaining, string token)
    {
        if (!remaining.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            return false;

        remaining = remaining[token.Length..];
        return true;
    }
}

/// <summary>
/// Represents the runner used for processes invoked with an argument list.
/// </summary>
internal delegate ValueTask<CommandExecutionResult> CommandArgumentsRunner(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the runner used for processes invoked with a raw argument string.
/// </summary>
internal delegate ValueTask<CommandExecutionResult> CommandTextRunner(
    string fileName,
    string arguments,
    string workingDirectory,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the provider that resolves the ripgrep executable path.
/// </summary>
internal delegate string RipgrepExecutablePathProvider();
