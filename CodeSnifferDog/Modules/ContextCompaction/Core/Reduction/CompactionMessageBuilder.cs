using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

internal static class CompactionMessageBuilder
{
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
