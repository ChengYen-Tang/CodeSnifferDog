using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Shell;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
[DoNotParallelize]
public sealed class ToolSetTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task PublicMethods_DelegateToService()
    {
        CommonCommandToolService service = new(
            CreateTemporaryDirectory(),
            new DelegateShellCommandRunner(static (command, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = $"shell:{command}",
                    StandardError = "",
                })),
            static (fileName, arguments, workingDirectory, cancellationToken) =>
                ValueTask.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = $"{fileName}:{arguments}",
                    StandardError = "",
                }),
            static () => "rg");
        string repositoryRootPath = CreateTemporaryDirectory();
        string targetFilePath = Path.Combine(repositoryRootPath, "sample.txt");
        await File.WriteAllTextAsync(targetFilePath, "one" + Environment.NewLine + "two", TestContext.CancellationToken);
        CommonToolSet toolSet = new(service, new CommonFileToolService(repositoryRootPath));

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
        CommandExecutionResult readResult = await toolSet.ReadFileRangeAsync(
            new ReadFileRangeArgs
            {
                Path = "sample.txt",
                OffsetLine = 2,
                LimitLines = 1,
            },
            TestContext.CancellationToken);

        Assert.AreEqual("shell:pwd", shellResult.StandardOutput);
        Assert.AreEqual("rg:alpha .", ripgrepResult.StandardOutput);
        Assert.AreEqual(0, readResult.ExitCode);
        Assert.AreEqual("two" + Environment.NewLine, readResult.StandardOutput);
    }

    [TestMethod]
    public async Task RunShellCommandAsync_ExecutesInsideRepositoryRootPath()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        CommonToolSet toolSet = new(repositoryRootPath);

        CommandExecutionResult result = await toolSet.RunShellCommandAsync(
            new RunShellCommandArgs
            {
                Command = "(Get-Location).Path",
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
            .Single(tool => string.Equals(tool.Name, "Ripgrep", StringComparison.Ordinal));

        Assert.Contains("use \"-n \\\"SystemPrompt\\\" .\"", ripgrepTool.Description);
        Assert.Contains("instead of \"rg -n \\\"SystemPrompt\\\" .\"", ripgrepTool.Description);
    }

    [TestMethod]
    public void CreateTools_IncludesReadFileRangeTool()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        CommonToolSet toolSet = new(repositoryRootPath);

        Microsoft.Extensions.AI.AITool readFileRangeTool = toolSet.CreateTools()
            .Single(tool => string.Equals(tool.Name, "ReadFileRange", StringComparison.Ordinal));

        Assert.Contains("Read a bounded line range from one file.", readFileRangeTool.Description);
    }

    [TestMethod]
    public async Task ReadFileRangeToolAsync_ReturnsCommandExecutionResultFormat()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        string targetFilePath = Path.Combine(repositoryRootPath, "sample.txt");
        await File.WriteAllTextAsync(
            targetFilePath,
            "one" + Environment.NewLine + "two",
            TestContext.CancellationToken);
        CommonToolSet toolSet = new(repositoryRootPath);

        AIFunction readFileRangeTool = Assert.IsInstanceOfType<AIFunction>(
            toolSet.CreateTools().Single(tool => string.Equals(tool.Name, "ReadFileRange", StringComparison.Ordinal)));

        object? result = await readFileRangeTool.InvokeAsync(
            new AIFunctionArguments
            {
                ["Path"] = "sample.txt",
                ["OffsetLine"] = 2,
                ["LimitLines"] = 1,
            },
            TestContext.CancellationToken);

        JsonElement jsonResult = Assert.IsInstanceOfType<JsonElement>(result);
        Assert.AreEqual(0, jsonResult.GetProperty("exitCode").GetInt32());
        Assert.AreEqual(string.Empty, jsonResult.GetProperty("standardError").GetString());
        string standardOutput = jsonResult.GetProperty("standardOutput").GetString()!;
        Assert.AreEqual("two" + Environment.NewLine, standardOutput);
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
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class DelegateShellCommandRunner(
        Func<string, string, CancellationToken, ValueTask<CommandExecutionResult>> run) : IShellCommandRunner
    {
        public ValueTask<CommandExecutionResult> RunAsync(
            string command,
            string workingDirectory,
            CancellationToken cancellationToken) => run(command, workingDirectory, cancellationToken);
    }
}
