using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Workflows.Common;

[TestClass]
public sealed class RetryLimitTests
{
    [TestMethod]
    public void IsReached_ReturnsFalse_ForZeroUnlimitedLimit()
    {
        Assert.IsFalse(RetryLimit.IsReached(100, 0));
    }

    [TestMethod]
    public void IsExceeded_ReturnsFalse_ForZeroUnlimitedLimit()
    {
        Assert.IsFalse(RetryLimit.IsExceeded(100, 0));
    }

    [TestMethod]
    public void IsReached_ReturnsTrue_WhenAttemptsReachPositiveLimit()
    {
        Assert.IsTrue(RetryLimit.IsReached(3, 3));
    }

    [TestMethod]
    public void NegativeLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RetryLimit.IsReached(1, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RetryLimit.IsExceeded(1, -1));
    }
}
