using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Executes shell and ripgrep commands for the common tool set.
/// </summary>
internal sealed class CommonCommandToolService
{
    private readonly string _repositoryRootPath;
    private readonly CommandArgumentsRunner _argumentsRunner;
    private readonly CommandTextRunner _textRunner;
    private readonly RipgrepExecutablePathProvider _ripgrepExecutablePathProvider;
    private readonly Func<bool> _isWindows;
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
            CommandProcessRunner.RunAsync,
            CommandProcessRunner.RunAsync,
            new RipgrepAssetLocator().GetExecutablePath,
            OperatingSystem.IsWindows,
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonCommandToolService"/> class for tests or composed services.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path used as the working directory.</param>
    /// <param name="argumentsRunner">Runner used for argument-list processes.</param>
    /// <param name="textRunner">Runner used for raw-argument-string processes.</param>
    /// <param name="ripgrepExecutablePathProvider">Provider that resolves the ripgrep executable path.</param>
    /// <param name="isWindows">Function that reports whether the current platform is Windows.</param>
    /// <param name="logger">Optional logger.</param>
    internal CommonCommandToolService(
        string repositoryRootPath,
        CommandArgumentsRunner argumentsRunner,
        CommandTextRunner textRunner,
        RipgrepExecutablePathProvider ripgrepExecutablePathProvider,
        Func<bool> isWindows,
        ILogger<CommonCommandToolService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(argumentsRunner);
        ArgumentNullException.ThrowIfNull(textRunner);
        ArgumentNullException.ThrowIfNull(ripgrepExecutablePathProvider);
        ArgumentNullException.ThrowIfNull(isWindows);

        _repositoryRootPath = ValidateRepositoryRootPath(repositoryRootPath);
        _argumentsRunner = argumentsRunner;
        _textRunner = textRunner;
        _ripgrepExecutablePathProvider = ripgrepExecutablePathProvider;
        _isWindows = isWindows;
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

        CommandExecutionResult result = _isWindows()
            ? await _argumentsRunner("powershell", ["-NoProfile", "-NonInteractive", "-EncodedCommand", EncodePowerShellCommand(BuildPowerShellCommand(command))], _repositoryRootPath, cancellationToken).ConfigureAwait(false)
            : await _argumentsRunner("/bin/bash", ["-lc", command], _repositoryRootPath, cancellationToken).ConfigureAwait(false);

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
    /// Encodes one PowerShell command for use with <c>-EncodedCommand</c>.
    /// </summary>
    /// <param name="command">Command text to encode.</param>
    /// <returns>The Base64-encoded command.</returns>
    private static string EncodePowerShellCommand(string command)
        =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

    /// <summary>
    /// Prepends standard PowerShell setup to one command.
    /// </summary>
    /// <param name="command">Command text to wrap.</param>
    /// <returns>The wrapped command.</returns>
    private static string BuildPowerShellCommand(string command)
        =>
        "$ProgressPreference = 'SilentlyContinue'" + Environment.NewLine + command;

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
