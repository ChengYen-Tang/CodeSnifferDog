using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Estimation;
using CodeSnifferDog.Modules.Tools.Output;
using CodeSnifferDog.Modules.Tools.Shell;
using System.Text.RegularExpressions;

namespace CodeSnifferDog.Tests.Modules.Tools.Shell;

[TestClass]
[DoNotParallelize]
public sealed class PowerShellCommandRunnerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_ExecutesPowerShellFromTheNuGetHostedRunspace()
    {
        PowerShellCommandRunner runner = new();
        string workingDirectory = CreateTemporaryDirectory();

        CommandExecutionResult result = await runner.RunAsync(
            "(Get-Location).Path",
            workingDirectory,
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Path.GetFullPath(workingDirectory), result.StandardOutput.Trim());
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_ResolvesBundledManagementModule()
    {
        PowerShellCommandRunner runner = new();

        CommandExecutionResult result = await runner.RunAsync(
            "(Get-Command Set-Location).Source",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Microsoft.PowerShell.Management", result.StandardOutput.Trim());
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_ReturnsPowerShellErrorsAsNonZeroResults()
    {
        PowerShellCommandRunner runner = new();

        CommandExecutionResult result = await runner.RunAsync(
            "Write-Error 'expected error'",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("expected error", result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_ReturnsTerminatingPowerShellErrorsAsNonZeroResults()
    {
        PowerShellCommandRunner runner = new();

        CommandExecutionResult result = await runner.RunAsync(
            "throw 'terminating error'",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("terminating error", result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_StopsThePipelineWhenCancelled()
    {
        PowerShellCommandRunner runner = new();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        try
        {
            _ = await runner.RunAsync(
                "Start-Sleep -Seconds 30",
                CreateTemporaryDirectory(),
                cancellation.Token);
            Assert.Fail("The PowerShell pipeline should have been cancelled.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    [TestMethod]
    public async Task RunAsync_WhenOutputExceedsLimit_StopsPipelineAndReturnsTruncationMetadata()
    {
        PowerShellCommandRunner runner = new();
        string markerPath = Path.Combine(CreateTemporaryDirectory(), "must-not-exist.txt");

        CommandExecutionResult result = await runner.RunAsync(
            $"$line = 'x' * 1024; 1..100000 | ForEach-Object {{ Write-Output $line; Write-Error $line }}; Set-Content -LiteralPath '{markerPath}' -Value 'executed'",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        int combinedBytes = TokenEstimator.GetUtf8ByteCount(result.StandardOutput) +
            TokenEstimator.GetUtf8ByteCount(result.StandardError);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsTrue(combinedBytes <= CommandOutputLimiter.MaxCombinedOutputBytes);
        Assert.Contains("Warning: command output was too large and was truncated.", result.StandardError);
        Match metadata = Regex.Match(
            result.StandardError,
            "Output observed before the pipeline was stopped. Lines: ([1-9][0-9]*), bytes: [1-9][0-9]*");

        Assert.IsTrue(metadata.Success);
        Assert.IsTrue(int.Parse(metadata.Groups[1].Value) < 200_000);
        Assert.IsFalse(File.Exists(markerPath));
        Assert.Contains("Do not retry the same large-output command with Shell.", result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_WhenCancelledBeforeInvocation_DoesNotExecuteTheCommand()
    {
        PowerShellCommandRunner runner = new();
        string markerPath = Path.Combine(CreateTemporaryDirectory(), "must-not-exist.txt");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await runner.RunAsync(
                $"Set-Content -LiteralPath '{markerPath}' -Value 'executed'",
                CreateTemporaryDirectory(),
                cancellation.Token));

        Assert.IsFalse(File.Exists(markerPath));
    }

    [TestMethod]
    public async Task RunAsync_RejectsDetachedProcessCommands()
    {
        PowerShellCommandRunner runner = new();

        CommandExecutionResult result = await runner.RunAsync(
            "Start-Process -FilePath 'does-not-matter'",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("Start-Process", result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_RejectsDotNetProcessDetachment()
    {
        PowerShellCommandRunner runner = new();

        CommandExecutionResult result = await runner.RunAsync(
            "[System.Diagnostics.Process]::Start('does-not-matter')",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("System.Diagnostics.Process", result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_RejectsDynamicCommandInvocation()
    {
        PowerShellCommandRunner runner = new();

        CommandExecutionResult result = await runner.RunAsync(
            "$command = 'Start-Process'; & $command -FilePath 'does-not-matter'",
            CreateTemporaryDirectory(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("dynamic command invocation", result.StandardError);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
