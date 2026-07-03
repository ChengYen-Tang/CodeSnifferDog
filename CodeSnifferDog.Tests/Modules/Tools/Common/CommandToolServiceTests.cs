using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Common;
using Microsoft.Extensions.Logging;
using System.Text;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
public sealed class CommandToolServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void Constructor_Throws_WhenRepositoryRootPathDoesNotExist()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.NewGuid().ToString("N"));

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => new CommonCommandToolService(missingPath));
    }

    [TestMethod]
    public async Task RunShellCommandAsync_UsesPowerShellEncodedCommand_OnWindows()
    {
        CapturedArgumentsRun captured = new();
        CommonCommandToolService service = CreateService(
            argumentsRunner: captured.RunAsync,
            textRunner: FailTextRunner,
            ripgrepExecutablePathProvider: () => "rg",
            isWindows: () => true);

        await service.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = "Get-Location",
            },
            TestContext.CancellationToken);

        Assert.AreEqual("powershell", captured.FileName);
        Assert.IsNotNull(captured.Arguments);
        CollectionAssert.AreEqual(
            new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand" },
            captured.Arguments.Take(3).ToArray());
        Assert.AreEqual(
            "$ProgressPreference = 'SilentlyContinue'" + Environment.NewLine + "Get-Location",
            Encoding.Unicode.GetString(Convert.FromBase64String(captured.Arguments[3])));
    }

    [TestMethod]
    public async Task RunShellCommandAsync_UsesBashLoginCommand_WhenNotWindows()
    {
        CapturedArgumentsRun captured = new();
        CommonCommandToolService service = CreateService(
            argumentsRunner: captured.RunAsync,
            textRunner: FailTextRunner,
            ripgrepExecutablePathProvider: () => "rg",
            isWindows: () => false);

        await service.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = "pwd",
            },
            TestContext.CancellationToken);

        Assert.AreEqual("/bin/bash", captured.FileName);
        CollectionAssert.AreEqual(new[] { "-lc", "pwd" }, captured.Arguments);
    }

    [TestMethod]
    public async Task RunShellCommandAsync_Throws_WhenCommandIsWhitespace()
    {
        CommonCommandToolService service = CreateService();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = " ",
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task RunRipgrepCommandAsync_RejectsRipgrepExecutablePrefix()
    {
        CommonCommandToolService service = CreateService();

        string[] commands =
        [
            "rg alpha .",
            "rg\talpha .",
            "rg.exe alpha .",
            "RG.EXE alpha .",
        ];

        foreach (string command in commands)
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RunRipgrepCommandAsync(
                new RunRipgrepCommandArgs
                {
                    Command = command,
                },
                TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task RunRipgrepCommandAsync_AllowsArgumentsAndAbsolutePath()
    {
        CapturedTextRun captured = new();
        CommonCommandToolService service = CreateService(
            argumentsRunner: FailArgumentsRunner,
            textRunner: captured.RunAsync,
            ripgrepExecutablePathProvider: () => @"Z:\Tools\rg.exe",
            isWindows: () => true);

        await service.RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = " alpha \"Z:\\outside\" ",
            },
            TestContext.CancellationToken);

        Assert.AreEqual(@"Z:\Tools\rg.exe", captured.FileName);
        Assert.AreEqual("alpha \"Z:\\outside\"", captured.Arguments);
    }

    [TestMethod]
    public async Task RunRipgrepCommandAsync_LogsNoMatchAsDebug()
    {
        CapturingLogger<CommonCommandToolService> logger = new();
        CommonCommandToolService service = CreateService(
            textRunner: static (fileName, arguments, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 1,
                    StandardOutput = "",
                    StandardError = "",
                }),
            logger: logger);

        await service.RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = "missing .",
            },
            TestContext.CancellationToken);

        CollectionAssert.DoesNotContain(logger.Levels, LogLevel.Warning);
        CollectionAssert.Contains(logger.Levels, LogLevel.Debug);
    }

    [TestMethod]
    public void RipgrepAssetLocator_GetExecutablePath_ThrowsOriginalMissingAssetMessage()
    {
        string baseDirectory = CreateTemporaryDirectory();
        RipgrepAssetLocator locator = new(baseDirectory);

        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(locator.GetExecutablePath);

        Assert.Contains("Ripgrep asset was not found under the application base directory.", exception.Message);
        Assert.Contains(Path.Combine(Path.GetFullPath(baseDirectory), "assets", "ripgrep"), exception.Message);
    }

    private static CommonCommandToolService CreateService(
        CommandArgumentsRunner? argumentsRunner = null,
        CommandTextRunner? textRunner = null,
        RipgrepExecutablePathProvider? ripgrepExecutablePathProvider = null,
        Func<bool>? isWindows = null,
        ILogger<CommonCommandToolService>? logger = null) =>
        new(
            CreateTemporaryDirectory(),
            argumentsRunner ?? SucceedArgumentsRunner,
            textRunner ?? SucceedTextRunner,
            ripgrepExecutablePathProvider ?? (() => "rg"),
            isWindows ?? (() => true),
            logger);

    private static ValueTask<CommandExecutionResult> SucceedArgumentsRunner(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeeded());

    private static ValueTask<CommandExecutionResult> SucceedTextRunner(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeeded());

    private static ValueTask<CommandExecutionResult> FailArgumentsRunner(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        throw new AssertFailedException("Arguments runner should not be called.");

    private static ValueTask<CommandExecutionResult> FailTextRunner(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        throw new AssertFailedException("Text runner should not be called.");

    private static CommandExecutionResult Succeeded() =>
        new()
        {
            ExitCode = 0,
            StandardOutput = "",
            StandardError = "",
        };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CapturedArgumentsRun
    {
        public string? FileName { get; private set; }

        public string[]? Arguments { get; private set; }

        public ValueTask<CommandExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            FileName = fileName;
            Arguments = [.. arguments];
            return ValueTask.FromResult(Succeeded());
        }
    }

    private sealed class CapturedTextRun
    {
        public string? FileName { get; private set; }

        public string? Arguments { get; private set; }

        public ValueTask<CommandExecutionResult> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            FileName = fileName;
            Arguments = arguments;
            return ValueTask.FromResult(Succeeded());
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
        }
    }
}
