using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class CollapseProjectionBuilder
{
    public static (IReadOnlyList<ChatMessage> Messages, IReadOnlyList<string> ProjectedCollapseIds) BuildProjection(
        IReadOnlyList<ChatMessage> messages,
        CollapseState collapseState,
        CompactionOptions options,
        bool includeStagedSpans = false)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(collapseState);
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<CollapseSpan> spans = GetProjectionSpans(collapseState, options, includeStagedSpans);
        if (spans.Count == 0)
            return (messages, []);

        IReadOnlyList<ResolvedProjectionSpan> resolvedSpans = ResolveProjectionSpans(messages, spans);
        if (resolvedSpans.Count == 0)
            return (messages, []);

        List<ChatMessage> projectedMessages = [];
        List<string> projectedCollapseIds = [];
        int nonSystemMessageIndex = 0;
        int spanIndex = 0;

        for (int index = 0; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];

            if (message.Role == ChatRole.System)
            {
                projectedMessages.Add(message);
                continue;
            }

            while (spanIndex < resolvedSpans.Count &&
                   nonSystemMessageIndex > resolvedSpans[spanIndex].LastResolvedMessageIndex)
                spanIndex++;

            if (spanIndex < resolvedSpans.Count &&
                nonSystemMessageIndex == resolvedSpans[spanIndex].FirstResolvedMessageIndex)
            {
                ResolvedProjectionSpan resolvedSpan = resolvedSpans[spanIndex];
                projectedMessages.Add(CreateProjectionMessage(resolvedSpan.Span));
                projectedMessages.Add(ContinuityStateBuilder.CreateProjectionMessage(
                    resolvedSpan.Span.ContinuityState,
                    resolvedSpan.Span.ContinuityProjectionMessageId,
                    resolvedSpan.Span.CollapseId,
                    resolvedSpan.Span.Reason));
                projectedCollapseIds.Add(resolvedSpan.Span.CollapseId);

                while (index + 1 < messages.Count &&
                       nonSystemMessageIndex < resolvedSpan.LastResolvedMessageIndex)
                {
                    index++;
                    if (messages[index].Role == ChatRole.System)
                        continue;

                    nonSystemMessageIndex++;
                }

                nonSystemMessageIndex++;

                continue;
            }

            if (spanIndex < resolvedSpans.Count &&
                nonSystemMessageIndex > resolvedSpans[spanIndex].FirstResolvedMessageIndex &&
                nonSystemMessageIndex <= resolvedSpans[spanIndex].LastResolvedMessageIndex)
            {
                nonSystemMessageIndex++;
                continue;
            }

            projectedMessages.Add(message);
            nonSystemMessageIndex++;
        }

        return (projectedMessages, projectedCollapseIds);
    }

    private static IReadOnlyList<CollapseSpan> GetProjectionSpans(
        CollapseState collapseState,
        CompactionOptions _,
        bool includeStagedSpans)
    {
        List<CollapseSpan> spans =
        [
            .. collapseState.Commits,
        ];

        if (includeStagedSpans)
            spans.AddRange(collapseState.StagedSpans);

        return [.. spans.OrderBy(static span => span.FirstArchivedMessageIndex)];
    }

    private static ChatMessage CreateProjectionMessage(CollapseSpan commit)
    {
        ChatMessage message = new(
            ChatRole.System,
            $"Collapsed context commit {commit.CollapseId}{Environment.NewLine}{Environment.NewLine}{commit.Summary}")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.CollapseProjectionArtifactKind,
                [CompactionArtifactMetadata.CollapseCommitIdKey] = commit.CollapseId,
                [CompactionArtifactMetadata.MessageIdentityKey] = commit.ProjectionMessageId,
                [CompactionArtifactMetadata.CompactionReasonKey] = commit.Reason,
                [CompactionArtifactMetadata.PreservedTailCountKey] = commit.ArchivedMessagesCount,
                [CompactionArtifactMetadata.PreservedSegmentHeadIndexKey] = commit.FirstArchivedMessageIndex,
                [CompactionArtifactMetadata.PreservedSegmentTailIndexKey] = commit.LastArchivedMessageIndex,
                [CompactionArtifactMetadata.PreservedSegmentHeadIdKey] = commit.FirstArchivedMessageId ?? string.Empty,
                [CompactionArtifactMetadata.PreservedSegmentTailIdKey] = commit.LastArchivedMessageId ?? string.Empty,
            },
        };

        return message;
    }

    private static IReadOnlyList<ResolvedProjectionSpan> ResolveProjectionSpans(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<CollapseSpan> spans)
    {
        List<ResolvedProjectionSpan> resolvedSpans = [];
        List<NonSystemMessageEntry> nonSystemMessages = [];

        for (int index = 0; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];
            if (message.Role == ChatRole.System)
                continue;

            nonSystemMessages.Add(new NonSystemMessageEntry
            {
                NonSystemIndex = nonSystemMessages.Count,
                MessageId = TryGetMessageIdentity(message),
            });
        }

        foreach (CollapseSpan span in spans)
        {
            int startIndex = ResolveBoundaryIndex(nonSystemMessages, span.FirstArchivedMessageId, span.FirstArchivedMessageIndex, 0);
            if (startIndex < 0)
                continue;

            int endIndex = ResolveBoundaryIndex(nonSystemMessages, span.LastArchivedMessageId, span.LastArchivedMessageIndex, startIndex);
            if (endIndex < startIndex)
                continue;

            resolvedSpans.Add(new ResolvedProjectionSpan
            {
                Span = span,
                FirstResolvedMessageIndex = startIndex,
                LastResolvedMessageIndex = endIndex,
            });
        }

        return [.. resolvedSpans.OrderBy(static span => span.FirstResolvedMessageIndex)];
    }

    private static int ResolveBoundaryIndex(
        IReadOnlyList<NonSystemMessageEntry> messages,
        string? messageId,
        int fallbackIndex,
        int startAt)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            for (int index = startAt; index < messages.Count; index++)
                if (string.Equals(messages[index].MessageId, messageId, StringComparison.Ordinal))
                    return messages[index].NonSystemIndex;
        }

        for (int index = startAt; index < messages.Count; index++)
            if (messages[index].NonSystemIndex == fallbackIndex)
                return messages[index].NonSystemIndex;

        return -1;
    }

    private static string? TryGetMessageIdentity(ChatMessage message)
    {
        if (message.AdditionalProperties?.TryGetValue(CompactionArtifactMetadata.MessageIdentityKey, out object? value) != true)
            return null;

        return value as string;
    }

    private sealed class NonSystemMessageEntry
    {
        public required int NonSystemIndex { get; init; }

        public string? MessageId { get; init; }
    }

    private sealed class ResolvedProjectionSpan
    {
        public required CollapseSpan Span { get; init; }

        public required int FirstResolvedMessageIndex { get; init; }

        public required int LastResolvedMessageIndex { get; init; }
    }
}
