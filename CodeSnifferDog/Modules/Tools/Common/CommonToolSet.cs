using CodeSnifferDog.Models.Common.Tools;
using System.Text;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Common;

public sealed class CommonToolSet(string repositoryRootPath)
{
    private readonly string _repositoryRootPath = ValidateRepositoryRootPath(repositoryRootPath);
    private readonly CommandProcessRunner _processRunner = new();
    private readonly RipgrepAssetLocator _ripgrepAssetLocator = new();

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(
            (Func<string, CancellationToken, ValueTask<CommandExecutionResult>>)RunShellCommandToolAsync,
            "RunShellCommand",
            "Run one shell command in the repository root path. Use PowerShell on Windows and bash on Linux/macOS. Pass only the command text to execute.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            (Func<string, CancellationToken, ValueTask<CommandExecutionResult>>)RunRipgrepCommandToolAsync,
            "RunRipgrepCommand",
            "Run one ripgrep search command in the repository root path. Pass only the arguments after rg. Do not include rg in the command text.",
            serializerOptions: null),
    ];

    private ValueTask<CommandExecutionResult> RunShellCommandToolAsync(
        string Command,
        CancellationToken cancellationToken) =>
        RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = Command,
            },
            cancellationToken);

    private ValueTask<CommandExecutionResult> RunRipgrepCommandToolAsync(
        string Command,
        CancellationToken cancellationToken) =>
        RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = Command,
            },
            cancellationToken);

    public ValueTask<CommandExecutionResult> RunShellCommandAsync(
        RunShellCommandArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Command);

        return OperatingSystem.IsWindows()
            ? _processRunner.RunAsync("powershell", ["-NoProfile", "-NonInteractive", "-EncodedCommand", EncodePowerShellCommand(args.Command)], _repositoryRootPath, cancellationToken)
            : _processRunner.RunAsync("/bin/bash", ["-lc", args.Command], _repositoryRootPath, cancellationToken);
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

        return _processRunner.RunAsync(
            _ripgrepAssetLocator.GetExecutablePath(),
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

    private static string EncodePowerShellCommand(string command) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

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
