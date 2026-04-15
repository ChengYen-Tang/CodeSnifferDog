using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class EstimatingOperationalContextCompactionUsageProvider(Func<string, int>? textToTokenEstimator = null)
    : IOperationalContextCompactionUsageProvider
{
    private readonly Func<string, int> _textToTokenEstimator = textToTokenEstimator ?? DefaultEstimate;

    public ValueTask<OperationalContextCompactionUsage?> GetUsageAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long usedTokens = 0;

        foreach (ChatMessage message in messages)
            usedTokens += _textToTokenEstimator(message.Text ?? string.Empty);

        return ValueTask.FromResult<OperationalContextCompactionUsage?>(new OperationalContextCompactionUsage
        {
            UsedTokens = usedTokens,
        });
    }

    private static int DefaultEstimate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }
}
