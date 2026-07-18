using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Shell;
using Microsoft.Extensions.Logging;

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
    public async Task RunShellCommandAsync_UsesInProcessPowerShellRunner_OnEveryPlatform()
    {
        CapturedShellRun captured = new();
        CommonCommandToolService service = CreateService(
            shellCommandRunner: captured,
            textRunner: FailTextRunner,
            ripgrepExecutablePathProvider: () => "rg");

        await service.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = "Get-Location",
            },
            TestContext.CancellationToken);

        Assert.AreEqual("Get-Location", captured.Command);
        Assert.AreEqual(service.RepositoryRootPath, captured.WorkingDirectory);
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
    public async Task RunShellCommandAsync_TruncatesCombinedOutputBeforeReturning()
    {
        string largeOutput = new('a', 120_000);
        CommonCommandToolService service = CreateService(
            shellCommandRunner: new DelegateShellCommandRunner((command, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = largeOutput,
                    StandardError = "error",
                })),
            textRunner: FailTextRunner);

        CommandExecutionResult result = await service.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = "cat huge.log",
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.StandardOutput.Length < largeOutput.Length);
        Assert.Contains("Warning: command output was too large and was truncated.", result.StandardError);
        Assert.Contains("Original lines: 2", result.StandardError);
        Assert.Contains("original bytes: 120005", result.StandardError);
        Assert.Contains("Do not retry the same large-output command with Shell.", result.StandardError);
        Assert.Contains("use ReadFileRange with smaller offsetLine/limitLines", result.StandardError);
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
            textRunner: captured.RunAsync,
            ripgrepExecutablePathProvider: () => @"Z:\Tools\rg.exe");

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
    public async Task RunRipgrepCommandAsync_TruncatesCombinedOutputBeforeReturning()
    {
        string largeOutput = string.Join(Environment.NewLine, Enumerable.Range(1, 30_000).Select(static index => $"match {index}"));
        CommonCommandToolService service = CreateService(
            textRunner: (fileName, arguments, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = largeOutput,
                    StandardError = "",
                }),
            ripgrepExecutablePathProvider: () => "rg");

        CommandExecutionResult result = await service.RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = ".",
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.StandardOutput.Length < largeOutput.Length);
        Assert.Contains("Warning: command output was too large and was truncated.", result.StandardError);
        Assert.Contains("Original lines: 30000", result.StandardError);
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
        IShellCommandRunner? shellCommandRunner = null,
        CommandTextRunner? textRunner = null,
        RipgrepExecutablePathProvider? ripgrepExecutablePathProvider = null,
        ILogger<CommonCommandToolService>? logger = null) =>
        new(
            CreateTemporaryDirectory(),
            shellCommandRunner ?? new DelegateShellCommandRunner(SucceedShellRunner),
            textRunner ?? SucceedTextRunner,
            ripgrepExecutablePathProvider ?? (() => "rg"),
            logger);

    private static ValueTask<CommandExecutionResult> SucceedShellRunner(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeeded());

    private static ValueTask<CommandExecutionResult> SucceedTextRunner(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeeded());

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

    private sealed class CapturedShellRun : IShellCommandRunner
    {
        public string? Command { get; private set; }

        public string? WorkingDirectory { get; private set; }

        public ValueTask<CommandExecutionResult> RunAsync(
            string command,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            Command = command;
            WorkingDirectory = workingDirectory;
            return ValueTask.FromResult(Succeeded());
        }
    }

    private sealed class DelegateShellCommandRunner(
        Func<string, string, CancellationToken, ValueTask<CommandExecutionResult>> run) : IShellCommandRunner
    {
        public ValueTask<CommandExecutionResult> RunAsync(
            string command,
            string workingDirectory,
            CancellationToken cancellationToken) => run(command, workingDirectory, cancellationToken);
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
