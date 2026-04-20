using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public interface IOperationalContextSummaryPromptProvider
{
    ValueTask<string> GetPromptAsync(CancellationToken cancellationToken);
}
