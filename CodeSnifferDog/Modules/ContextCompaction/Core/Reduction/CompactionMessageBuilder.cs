using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Reconstructs the active transcript from a <see cref="CompactionResult" />.
/// </summary>
internal static class CompactionMessageBuilder
{
    /// <summary>
    /// Builds the ordered message list that should remain active after compaction.
    /// </summary>
    /// <param name="result">Compaction result containing system artifacts, preserved tail messages, and carry-forward metadata artifacts.</param>
    /// <returns>The ordered compacted transcript.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<ChatMessage> Build(CompactionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        List<ChatMessage> messages = [.. result.PreservedSystemMessages];

        if (!string.IsNullOrWhiteSpace(result.BoundaryMessage.Text))
            messages.Add(result.BoundaryMessage);

        if (!string.IsNullOrWhiteSpace(result.SummaryMessage.Text))
            messages.Add(result.SummaryMessage);

        if (!string.IsNullOrWhiteSpace(result.ContinuityStateMessage.Text))
            messages.Add(result.ContinuityStateMessage);

        messages.AddRange(result.MessagesToKeep);
        messages.AddRange(result.AttachmentMessages);
        messages.AddRange(result.HookResultMessages);

        return messages;
    }
}
