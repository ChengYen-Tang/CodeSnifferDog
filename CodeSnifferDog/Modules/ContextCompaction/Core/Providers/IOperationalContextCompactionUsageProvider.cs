using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public interface IOperationalContextCompactionUsageProvider
{
    ValueTask<OperationalContextCompactionUsage?> GetUsageAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);
}
