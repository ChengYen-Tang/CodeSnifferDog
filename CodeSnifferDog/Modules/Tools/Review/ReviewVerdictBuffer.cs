using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Modules.Tools.Review;

public sealed class ReviewVerdictBuffer
{
    public ReviewVerdict? Latest { get; private set; }

    public void Reset() => Latest = null;

    public void Submit(bool approved, string message) =>
        Latest = new ReviewVerdict
        {
            Approved = approved,
            Message = message,
        };
}
