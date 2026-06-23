using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review;

namespace CodeSnifferDog.Tests.Modules.Tools.Review;

[TestClass]
public sealed class ReviewVerdictToolServiceTests
{
    [TestMethod]
    public async Task SubmitReviewVerdictAsync_TrimsMessageAndReturnsTrue()
    {
        ReviewVerdictBuffer buffer = new();
        ReviewVerdictToolService service = new(buffer);

        bool result = await service.SubmitReviewVerdictAsync(new SubmitReviewVerdictArgs
        {
            Approved = true,
            Message = " approved ",
        });

        ReviewVerdict? verdict = buffer.Latest;
        Assert.IsTrue(result);
        Assert.IsNotNull(verdict);
        Assert.IsTrue(verdict.Approved);
        Assert.AreEqual("approved", verdict.Message);
    }

    [TestMethod]
    public void SubmitReviewVerdictAsync_Throws_WhenMessageIsEmpty()
    {
        ReviewVerdictToolService service = new(new ReviewVerdictBuffer());

        Assert.ThrowsExactly<ArgumentException>(() =>
            service.SubmitReviewVerdictAsync(new SubmitReviewVerdictArgs
            {
                Approved = false,
                Message = " ",
            }).GetAwaiter().GetResult());
    }
}
