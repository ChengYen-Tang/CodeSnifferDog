using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Output;
using SharedTokenEstimator = CodeSnifferDog.Modules.Estimation.TokenEstimator;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
public sealed class CommandProcessRunnerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_WithLargeStdoutAndStderr_ReturnsBoundedOutputWithWarning()
    {
        string workingDirectory = CreateTemporaryDirectory();
        CommandExecution command = CreateLargeOutputCommand();

        CodeSnifferDog.Models.Common.Tools.CommandExecutionResult result =
            await CommandProcessRunner.RunAsync(
                command.FileName,
                command.Arguments,
                workingDirectory,
                TestContext.CancellationToken);

        int combinedBytes = SharedTokenEstimator.GetUtf8ByteCount(result.StandardOutput) +
            SharedTokenEstimator.GetUtf8ByteCount(result.StandardError);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(combinedBytes <= CommandOutputLimiter.MaxCombinedOutputBytes);
        Assert.Contains("Warning: command output was too large and was truncated.", result.StandardError);
        Assert.Contains("Original lines: 1600", result.StandardError);
        Assert.Contains("Do not retry the same large-output command with Shell.", result.StandardError);
        Assert.Contains("use ReadFileRange with smaller offsetLine/limitLines", result.StandardError);
    }

    /// <summary>
    /// Creates a platform-specific command that emits enough stdout and stderr to trigger truncation.
    /// </summary>
    /// <returns>The command execution descriptor.</returns>
    private static CommandExecution CreateLargeOutputCommand()
    {
        if (OperatingSystem.IsWindows())
        {
            return new CommandExecution(
                "powershell",
                [
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    "$out = 'o' * 200; $err = 'e' * 200; 1..800 | ForEach-Object { [Console]::Out.WriteLine($out); [Console]::Error.WriteLine($err) }",
                ]);
        }

        return new CommandExecution(
            "/bin/bash",
            [
                "-lc",
                "out=$(printf 'o%.0s' {1..200}); err=$(printf 'e%.0s' {1..200}); for i in $(seq 1 800); do printf '%s\\n' \"$out\"; printf '%s\\n' \"$err\" >&2; done",
            ]);
    }

    /// <summary>
    /// Creates a temporary working directory for process execution.
    /// </summary>
    /// <returns>The created directory path.</returns>
    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Describes a process invocation used by the integration test.
    /// </summary>
    /// <param name="FileName">Executable file name.</param>
    /// <param name="Arguments">Executable arguments.</param>
    private sealed record CommandExecution(
        string FileName,
        IReadOnlyList<string> Arguments);
}
