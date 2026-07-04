using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Replaces archived non-system message spans with compact collapse projection artifacts.
/// </summary>
public sealed class CollapseProjectionBuilder
{
    /// <summary>
    /// Builds a projected transcript by substituting committed, and optionally staged, collapse spans into the message list.
    /// </summary>
    /// <param name="messages">Current transcript messages before projection.</param>
    /// <param name="collapseState">Collapse state that describes committed and staged spans.</param>
    /// <param name="options">Compaction options associated with the projection.</param>
    /// <param name="includeStagedSpans"><see langword="true" /> to project staged spans in addition to committed spans.</param>
    /// <returns>The projected messages and the collapse identifiers that were injected into the transcript.</returns>
    /// <remarks>
    /// Projection operates only on non-system message indexes so existing system artifacts remain in place.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="messages" />, <paramref name="collapseState" />, or <paramref name="options" /> is <see langword="null" />.</exception>
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

    /// <summary>
    /// Returns the collapse spans that should be projected, ordered by their archived start index.
    /// </summary>
    /// <param name="collapseState">Collapse state containing committed and staged spans.</param>
    /// <param name="_">Unused compaction options parameter retained for API stability.</param>
    /// <param name="includeStagedSpans"><see langword="true" /> to include staged spans in the returned projection set.</param>
    /// <returns>The ordered collapse spans to apply to the transcript.</returns>
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

    /// <summary>
    /// Creates the system projection artifact that stands in for one collapsed transcript span.
    /// </summary>
    /// <param name="commit">Committed or staged collapse span being projected.</param>
    /// <returns>A system message annotated with collapse projection metadata.</returns>
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

    /// <summary>
    /// Resolves collapse span boundaries against the current non-system transcript so projections can survive message reindexing.
    /// </summary>
    /// <param name="messages">Current transcript messages.</param>
    /// <param name="spans">Collapse spans to resolve.</param>
    /// <returns>The collapse spans whose boundaries could be resolved against the current transcript.</returns>
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

    /// <summary>
    /// Resolves one collapse boundary by stable message identifier when possible, otherwise by fallback non-system index.
    /// </summary>
    /// <param name="messages">Non-system transcript entries available for resolution.</param>
    /// <param name="messageId">Preferred stable message identifier.</param>
    /// <param name="fallbackIndex">Fallback non-system index recorded in the collapse span.</param>
    /// <param name="startAt">First entry position to consider while searching.</param>
    /// <returns>The resolved non-system index, or <c>-1</c> when no match is found.</returns>
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

    /// <summary>
    /// Reads the synthetic identity assigned to a message, if one exists.
    /// </summary>
    /// <param name="message">Message whose identity metadata should be inspected.</param>
    /// <returns>The stored message identity, or <see langword="null" /> when the message has not been annotated.</returns>
    private static string? TryGetMessageIdentity(ChatMessage message)
    {
        if (message.AdditionalProperties?.TryGetValue(CompactionArtifactMetadata.MessageIdentityKey, out object? value) != true)
            return null;

        return value as string;
    }

    /// <summary>
    /// Represents one non-system transcript entry available for collapse-boundary resolution.
    /// </summary>
    private sealed class NonSystemMessageEntry
    {
        public required int NonSystemIndex { get; init; }

        public string? MessageId { get; init; }
    }

    /// <summary>
    /// Represents a collapse span whose boundaries have been resolved against the current transcript.
    /// </summary>
    private sealed class ResolvedProjectionSpan
    {
        public required CollapseSpan Span { get; init; }

        public required int FirstResolvedMessageIndex { get; init; }

        public required int LastResolvedMessageIndex { get; init; }
    }
}
