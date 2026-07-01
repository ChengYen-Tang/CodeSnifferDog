using CodeSnifferDog.Models.Common.Tools;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CodeSnifferDog.Modules.Tools.Common;

internal sealed class CommonCommandToolService
{
    private readonly string _repositoryRootPath;
    private readonly CommandArgumentsRunner _argumentsRunner;
    private readonly CommandTextRunner _textRunner;
    private readonly RipgrepExecutablePathProvider _ripgrepExecutablePathProvider;
    private readonly Func<bool> _isWindows;
    private readonly ILogger<CommonCommandToolService>? _logger;

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

        LogCommandResult("Shell", command, result, stopwatch.ElapsedMilliseconds);
        return result;
    }

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

        LogCommandResult("Ripgrep", trimmedCommand, result, stopwatch.ElapsedMilliseconds);
        return result;
    }

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

    private static string ValidateRepositoryRootPath(string repositoryRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        string fullPath = Path.GetFullPath(repositoryRootPath.Trim());

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Repository root path does not exist: {fullPath}");

        return fullPath;
    }

    private static string EncodePowerShellCommand(string command)
        =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

    private static string BuildPowerShellCommand(string command)
        =>
        "$ProgressPreference = 'SilentlyContinue'" + Environment.NewLine + command;

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

    private static bool TryConsumeToken(ref ReadOnlySpan<char> remaining, string token)
    {
        if (!remaining.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            return false;

        remaining = remaining[token.Length..];
        return true;
    }
}

internal delegate ValueTask<CommandExecutionResult> CommandArgumentsRunner(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    CancellationToken cancellationToken);

internal delegate ValueTask<CommandExecutionResult> CommandTextRunner(
    string fileName,
    string arguments,
    string workingDirectory,
    CancellationToken cancellationToken);

internal delegate string RipgrepExecutablePathProvider();
