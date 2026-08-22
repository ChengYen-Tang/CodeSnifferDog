using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Selects the recent transcript messages that remain active after a full compaction pass.
/// </summary>
/// <remarks>
/// The selector owns the tail policy boundary. Implementations may use different grouping
/// mechanics, but must preserve whole tool-call groups and return messages in transcript order.
/// </remarks>
internal interface ICompactionTailSelector
{
    /// <summary>
    /// Selects the messages that satisfy the configured preserved-tail policy.
    /// </summary>
    /// <param name="nonSystemMessages">Non-system transcript messages in their original order.</param>
    /// <param name="options">Compaction options that define the preserved-tail limits.</param>
    /// <param name="cancellationToken">Cancels asynchronous grouping or selection work.</param>
    /// <returns>The messages that should remain active after compaction.</returns>
    ValueTask<IReadOnlyList<ChatMessage>> SelectAsync(
        IReadOnlyList<ChatMessage> nonSystemMessages,
        CompactionOptions options,
        CancellationToken cancellationToken);
}
