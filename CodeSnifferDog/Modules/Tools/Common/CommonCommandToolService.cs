using CodeSnifferDog.Models.Common.Tools;
using System.Text;

namespace CodeSnifferDog.Modules.Tools.Common;

internal sealed class CommonCommandToolService
{
    private readonly string _repositoryRootPath;
    private readonly CommandArgumentsRunner _argumentsRunner;
    private readonly CommandTextRunner _textRunner;
    private readonly RipgrepExecutablePathProvider _ripgrepExecutablePathProvider;
    private readonly Func<bool> _isWindows;

    public CommonCommandToolService(string repositoryRootPath)
        : this(
            repositoryRootPath,
            CommandProcessRunner.RunAsync,
            CommandProcessRunner.RunAsync,
            new RipgrepAssetLocator().GetExecutablePath,
            OperatingSystem.IsWindows)
    {
    }

    internal CommonCommandToolService(
        string repositoryRootPath,
        CommandArgumentsRunner argumentsRunner,
        CommandTextRunner textRunner,
        RipgrepExecutablePathProvider ripgrepExecutablePathProvider,
        Func<bool> isWindows)
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
    }

    public ValueTask<CommandExecutionResult> RunShellCommandAsync(
        RunShellCommandArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Command);

        return _isWindows()
            ? _argumentsRunner("powershell", ["-NoProfile", "-NonInteractive", "-EncodedCommand", EncodePowerShellCommand(BuildPowerShellCommand(args.Command))], _repositoryRootPath, cancellationToken)
            : _argumentsRunner("/bin/bash", ["-lc", args.Command], _repositoryRootPath, cancellationToken);
    }

    public ValueTask<CommandExecutionResult> RunRipgrepCommandAsync(
        RunRipgrepCommandArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Command);

        string trimmedCommand = args.Command.Trim();

        if (StartsWithRipgrepExecutable(trimmedCommand))
            throw new ArgumentException("Command must not include the rg executable name.", nameof(args));

        return _textRunner(
            _ripgrepExecutablePathProvider(),
            trimmedCommand,
            _repositoryRootPath,
            cancellationToken);
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
