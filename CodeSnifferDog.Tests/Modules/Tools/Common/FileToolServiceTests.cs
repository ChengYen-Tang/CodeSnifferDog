using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Tools.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
public sealed class FileToolServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ReadFileRangeAsync_ReturnsRequestedLineRange()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(repositoryRootPath, "sample.txt");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine, ["one", "two", "three", "four"]),
            TestContext.CancellationToken);
        CommonFileToolService service = new(repositoryRootPath);

        CommandExecutionResult result = await service.ReadFileRangeAsync(
            new ReadFileRangeArgs
            {
                Path = "sample.txt",
                OffsetLine = 2,
                LimitLines = 2,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardError);
        Assert.AreEqual("two" + Environment.NewLine + "three" + Environment.NewLine, result.StandardOutput);
    }

    [TestMethod]
    public async Task ReadFileRangeAsync_WhenRequestedRangeIsTooLarge_ReturnsShortErrorWithoutContent()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(repositoryRootPath, "large.txt");
        string[] lines = [.. Enumerable.Range(1, 20_000).Select(static index => $"line {index} {new string('x', 20)}")];
        await File.WriteAllLinesAsync(filePath, lines, TestContext.CancellationToken);
        CommonFileToolService service = new(repositoryRootPath);

        CommandExecutionResult result = await service.ReadFileRangeAsync(
            new ReadFileRangeArgs
            {
                Path = "large.txt",
                OffsetLine = 1,
                LimitLines = 20_000,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        Assert.Contains("Requested file range is too large to return safely.", result.StandardError);
        Assert.Contains("Original lines: at least", result.StandardError);
        Assert.Contains("Use ReadFileRange with a smaller offsetLine/limitLines.", result.StandardError);
    }

    [TestMethod]
    public async Task ReadFileRangeAsync_WhenFileIsMissing_ReturnsShortError()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        CommonFileToolService service = new(repositoryRootPath);

        CommandExecutionResult result = await service.ReadFileRangeAsync(
            new ReadFileRangeArgs
            {
                Path = "missing.txt",
                OffsetLine = 1,
                LimitLines = 20,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        Assert.Contains("File not found:", result.StandardError);
    }

    [TestMethod]
    public async Task ReadFileRangeAsync_AllowsAbsolutePathOutsideRepositoryRootPath()
    {
        string repositoryRootPath = CreateTemporaryDirectory();
        string externalDirectoryPath = CreateTemporaryDirectory();
        string externalFilePath = Path.Combine(externalDirectoryPath, "third-party.txt");
        await File.WriteAllTextAsync(
            externalFilePath,
            "external one" + Environment.NewLine + "external two",
            TestContext.CancellationToken);
        CommonFileToolService service = new(repositoryRootPath);

        CommandExecutionResult result = await service.ReadFileRangeAsync(
            new ReadFileRangeArgs
            {
                Path = externalFilePath,
                OffsetLine = 2,
                LimitLines = 1,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardError);
        Assert.AreEqual("external two" + Environment.NewLine, result.StandardOutput);
    }

    /// <summary>
    /// Creates a temporary directory for range reader tests.
    /// </summary>
    /// <returns>The created directory path.</returns>
    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeSnifferDog.Tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
