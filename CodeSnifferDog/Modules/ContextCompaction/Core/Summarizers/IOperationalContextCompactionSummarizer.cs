using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;

public interface IOperationalContextCompactionSummarizer
{
    ValueTask<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messages,
        string summaryPrompt,
        OperationalContextCompactionOptions options,
        CancellationToken cancellationToken);
}
