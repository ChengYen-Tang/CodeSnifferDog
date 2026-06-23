using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
[DoNotParallelize]
public sealed class CommonToolSetTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task PublicMethods_DelegateToService()
    {
        CommonCommandToolService service = new(
            CreateTemporaryDirectory(),
            static (fileName, arguments, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = $"{fileName}:{arguments[0]}",
                    StandardError = "",
                }),
            static (fileName, arguments, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = $"{fileName}:{arguments}",
                    StandardError = "",
                }),
            static () => "rg",
            static () => false);
        CommonToolSet toolSet = new(service);

        CommandExecutionResult shellResult = await toolSet.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = "pwd",
            },
            TestContext.CancellationToken);
        CommandExecutionResult ripgrepResult = await toolSet.RunRipgrepCommandAsync(
            new RunRipgrepCommandArgs
            {
                Command = "alpha .",
            },
            TestContext.CancellationToken);

        Assert.AreEqual("/bin/bash:-lc", shellResult.StandardOutput);
        Assert.AreEqual("rg:alpha .", ripgrepResult.StandardOutput);
    }

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
        Assert.Contains("sample.txt", result.StandardOutput);
        Assert.Contains("alpha beta gamma", result.StandardOutput);
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
        Assert.Contains("external.txt", result.StandardOutput);
        Assert.Contains("external alpha", result.StandardOutput);
    }

    [TestMethod]
    public void CreateTools_RipgrepToolDescriptionIncludesArgumentOnlyExample()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        CommonToolSet toolSet = new(repositoryRootPath);

        Microsoft.Extensions.AI.AITool ripgrepTool = toolSet.CreateTools()
            .Single(tool => string.Equals(tool.Name, "RunRipgrepCommand", StringComparison.Ordinal));

        Assert.Contains("use \"-n \\\"SystemPrompt\\\" .\"", ripgrepTool.Description);
        Assert.Contains("instead of \"rg -n \\\"SystemPrompt\\\" .\"", ripgrepTool.Description);
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
        Assert.IsTrue(
            executablePath.StartsWith(
                Path.Combine(Path.GetFullPath(AppContext.BaseDirectory), "assets", "ripgrep"),
                StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
