using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
[DoNotParallelize]
public sealed class CommonToolSetTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunShellCommandAsync_ExecutesInsideRepositoryRootPath()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        CommonToolSet toolSet = new(repositoryRootPath);

        CommandExecutionResult result = await toolSet.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = OperatingSystem.IsWindows()
                    ? "(Get-Location).Path"
                    : "pwd",
            },
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Path.GetFullPath(repositoryRootPath), result.StandardOutput.Trim());
    }

    [TestMethod]
    public async Task RunRipgrepCommandAsync_FindsMatchesInsideRepositoryRootPath()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        string targetFilePath = Path.Combine(repositoryRootPath, "sample.txt");
        await File.WriteAllTextAsync(targetFilePath, "alpha beta gamma", TestContext.CancellationToken);
        CommonToolSet toolSet = new(repositoryRootPath);

        CommandExecutionResult result = await toolSet.RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = "alpha .",
            },
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "sample.txt");
        StringAssert.Contains(result.StandardOutput, "alpha beta gamma");
    }

    [TestMethod]
    public async Task RunRipgrepCommandAsync_AllowsAbsolutePathOutsideRepositoryRootPath()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        string externalDirectoryPath = CreateTemporaryDirectory();
        string targetFilePath = Path.Combine(externalDirectoryPath, "external.txt");
        await File.WriteAllTextAsync(targetFilePath, "external alpha", TestContext.CancellationToken);
        CommonToolSet toolSet = new(repositoryRootPath);

        CommandExecutionResult result = await toolSet.RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = $"alpha \"{externalDirectoryPath}\"",
            },
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "external.txt");
        StringAssert.Contains(result.StandardOutput, "external alpha");
    }

    [TestMethod]
    public async Task RunRipgrepCommandAsync_RejectsRipgrepExecutablePrefix()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        CommonToolSet toolSet = new(repositoryRootPath);

        string[] commands =
        [
            "rg alpha .",
            "rg\talpha .",
            "rg.exe alpha .",
            "RG.EXE alpha .",
        ];

        foreach (string command in commands)
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.RunRipgrepCommandAsync(
                new RunRipgrepCommandArgs
                {
                    Command = command,
                },
                TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public void RipgrepAssetLocator_GetExecutablePath_ReturnsExistingFile()
    {
        RipgrepAssetLocator locator = new();

        string executablePath = locator.GetExecutablePath();

        Assert.IsTrue(File.Exists(executablePath));
        StringAssert.StartsWith(executablePath, Path.Combine(Path.GetFullPath(AppContext.BaseDirectory), "assets", "ripgrep"), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
