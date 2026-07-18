using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.Output;

namespace CodeSnifferDog.Tests.Modules.Tools.Common;

[TestClass]
public sealed class CommandStreamCaptureTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ReadAsync_WhenStreamExceedsBudget_CapturesPrefixAndCountsOriginalOutput()
    {
        string output = string.Join(Environment.NewLine, Enumerable.Range(1, 30_000).Select(static index => $"line {index}"));
        CommandOutputCaptureBudget budget = new(1_024);

        CommandStreamCapture capture = await CommandStreamCapture.ReadAsync(
            new StringReader(output),
            budget,
            TestContext.CancellationToken);

        Assert.IsTrue(capture.WasTruncated);
        Assert.IsTrue(capture.CapturedText.Length < output.Length);
        Assert.AreEqual(30_000, capture.OriginalLines);
        Assert.AreEqual(CodeSnifferDog.Modules.Estimation.TokenEstimator.GetUtf8ByteCount(output), capture.OriginalBytes);
    }

    [TestMethod]
    public async Task ReadAsync_SharesBudgetAcrossStreams()
    {
        CommandOutputCaptureBudget budget = new(10);

        CommandStreamCapture first = await CommandStreamCapture.ReadAsync(
            new StringReader("12345678"),
            budget,
            TestContext.CancellationToken);
        CommandStreamCapture second = await CommandStreamCapture.ReadAsync(
            new StringReader("abcdef"),
            budget,
            TestContext.CancellationToken);

        Assert.AreEqual("12345678", first.CapturedText);
        Assert.AreEqual("ab", second.CapturedText);
        Assert.IsFalse(first.WasTruncated);
        Assert.IsTrue(second.WasTruncated);
    }
}
