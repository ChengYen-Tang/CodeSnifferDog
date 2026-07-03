using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Shrinking;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class MessageShrinker
{
    // This stage intentionally operates at the local message layer because the current
    // Microsoft Agent Framework abstraction does not expose Claude-style API-layer
    // cache edits. It preserves the shrinking intent, but it is not a byte-for-byte
    // equivalent of Claude Code's primary microcompact path.
    public static MessageShrinkResult ApplyMicroCompaction(
        IReadOnlyList<ChatMessage> messages,
        CompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableMicroCompaction)
            return MessageShrinkResult.NoChange(messages);

        ShrinkPlan plan = CreatePlan(
            messages,
            options,
            options.MicroCompactTriggerToolResultCount,
            options.MicroCompactKeepRecentToolResultCount);

        if (plan.Candidates.Count <= plan.KeepRecentCount)
            return MessageShrinkResult.NoChange(messages);

        HashSet<string> toolCallIdsToClear = [.. plan.Candidates
            .Take(plan.Candidates.Count - plan.KeepRecentCount)
            .Select(static candidate => candidate.CallId)];

        List<ToolResultCandidate> candidatesToClear =
            [.. plan.Candidates.Where(candidate => toolCallIdsToClear.Contains(candidate.CallId))];

        return RewriteMessages(
            messages,
            candidatesToClear,
            removeCompactedMessages: false,
            "microcompact");
    }

    public static MessageShrinkResult ApplySnip(
        IReadOnlyList<ChatMessage> messages,
        CompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableSnip)
            return MessageShrinkResult.NoChange(messages);

        ShrinkPlan plan = CreatePlan(
            messages,
            options,
            options.SnipTriggerToolResultCount,
            options.SnipKeepRecentToolResultCount);

        if (plan.Candidates.Count <= plan.KeepRecentCount)
            return MessageShrinkResult.NoChange(messages);

        HashSet<string> toolCallIdsToRemove = [.. plan.Candidates
            .Take(plan.Candidates.Count - plan.KeepRecentCount)
            .Select(static candidate => candidate.CallId)];

        List<ToolResultCandidate> candidatesToRemove =
            [.. plan.Candidates.Where(candidate => toolCallIdsToRemove.Contains(candidate.CallId))];

        MessageShrinkResult rewriteResult = RewriteMessages(
            messages,
            candidatesToRemove,
            removeCompactedMessages: true,
            "snip");

        if (!rewriteResult.WasChanged)
            return rewriteResult;

        ChatMessage boundaryMessage = CreateSnipBoundaryMessage(
            rewriteResult.ShrunkToolResultCount,
            rewriteResult.FreedEstimatedTokens);
        List<ChatMessage> rewrittenMessages = [boundaryMessage, .. rewriteResult.Messages];

        return new MessageShrinkResult
        {
            Messages = rewrittenMessages,
            FreedEstimatedTokens = rewriteResult.FreedEstimatedTokens,
            ShrunkToolResultCount = rewriteResult.ShrunkToolResultCount,
        };
    }

    private static ShrinkPlan CreatePlan(
        IReadOnlyList<ChatMessage> messages,
        CompactionOptions options,
        int triggerCount,
        int keepRecentCount)
    {
        int normalizedKeepRecentCount = Math.Max(1, keepRecentCount);
        if (triggerCount <= normalizedKeepRecentCount)
            triggerCount = normalizedKeepRecentCount + 1;

        Dictionary<string, string> compactableCallIds = [];
        List<ToolResultCandidate> candidates = [];

        foreach (ChatMessage message in messages)
        {
            foreach (AIContent content in message.Contents)
            {
                if (content is FunctionCallContent functionCall &&
                    options.CompactableToolNames.Contains(functionCall.Name, StringComparer.Ordinal))
                {
                    compactableCallIds[functionCall.CallId] = functionCall.Name;
                    continue;
                }

                if (content is FunctionResultContent functionResult &&
                    compactableCallIds.TryGetValue(functionResult.CallId, out string? toolName))
                {
                    candidates.Add(new ToolResultCandidate(
                        functionResult.CallId,
                        toolName,
                        message,
                        functionResult));
                }
            }
        }

        return candidates.Count < triggerCount
            ? new ShrinkPlan([], normalizedKeepRecentCount)
            : new ShrinkPlan(candidates, normalizedKeepRecentCount);
    }

    private static MessageShrinkResult RewriteMessages(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolResultCandidate> candidates,
        bool removeCompactedMessages,
        string shrinkOperation)
    {
        Dictionary<string, ToolResultCandidate> candidateMap = candidates.ToDictionary(candidate => candidate.CallId, StringComparer.Ordinal);
        HashSet<string> targetCallIds = [.. candidateMap.Keys];
        List<ChatMessage> rewrittenMessages = [];
        int freedEstimatedTokens = 0;
        int shrunkToolResultCount = 0;

        foreach (ChatMessage message in messages)
        {
            bool changed = false;
            List<AIContent> rewrittenContents = [];

            foreach (AIContent content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent functionCall when
                        removeCompactedMessages &&
                        targetCallIds.Contains(functionCall.CallId):
                        freedEstimatedTokens += TokenEstimator.EstimateContent(content);
                        changed = true;
                        continue;

                    case FunctionResultContent functionResult when targetCallIds.Contains(functionResult.CallId):
                        freedEstimatedTokens += TokenEstimator.EstimateContent(content);
                        shrunkToolResultCount++;
                        changed = true;

                        if (removeCompactedMessages)
                            continue;

                        ToolResultCandidate candidate = candidateMap[functionResult.CallId];
                        rewrittenContents.Add(new FunctionResultContent(
                            functionResult.CallId,
                            CreateMicroCompactedToolResultArtifact(candidate.ToolName, functionResult.CallId)));
                        break;

                    default:
                        rewrittenContents.Add(content);
                        break;
                }
            }

            if (!changed)
            {
                rewrittenMessages.Add(message);
                continue;
            }

            if (rewrittenContents.Count == 0 && string.IsNullOrWhiteSpace(message.Text))
                continue;

            rewrittenMessages.Add(CloneMessage(message, rewrittenContents, shrinkOperation, shrunkToolResultCount, freedEstimatedTokens));
        }

        return new MessageShrinkResult
        {
            Messages = rewrittenMessages,
            FreedEstimatedTokens = freedEstimatedTokens,
            ShrunkToolResultCount = shrunkToolResultCount,
        };
    }

    private static ChatMessage CloneMessage(
        ChatMessage original,
        List<AIContent> contents,
        string shrinkOperation,
        int shrunkToolResultCount,
        int freedEstimatedTokens)
    {
        ChatMessage clone = contents.Count == 0
            ? new ChatMessage(original.Role, original.Text ?? string.Empty)
            : new ChatMessage(original.Role, [.. contents]);

        clone.AdditionalProperties = original.AdditionalProperties?.Count > 0
            ? new AdditionalPropertiesDictionary(original.AdditionalProperties)
            : [];
        clone.AdditionalProperties[CompactionArtifactMetadata.ShrinkOperationKey] = shrinkOperation;
        clone.AdditionalProperties[CompactionArtifactMetadata.ShrunkToolResultCountKey] = shrunkToolResultCount;
        clone.AdditionalProperties[CompactionArtifactMetadata.FreedEstimatedTokensKey] = freedEstimatedTokens;

        return clone;
    }

    private static string CreateMicroCompactedToolResultArtifact(string toolName, string callId) =>
        $$"""
        [Compacted tool result]
        Tool: {{toolName}}
        CallId: {{callId}}
        Reason: framework-level-microcompact
        Omitted: raw tool output removed to reduce context pressure
        """;

    private static ChatMessage CreateSnipBoundaryMessage(int shrunkToolResultCount, int freedEstimatedTokens)
    {
        ChatMessage boundaryMessage = new(ChatRole.System, "Operational snip boundary")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.SnipBoundaryArtifactKind,
                [CompactionArtifactMetadata.ShrinkOperationKey] = "snip",
                [CompactionArtifactMetadata.ShrunkToolResultCountKey] = shrunkToolResultCount,
                [CompactionArtifactMetadata.FreedEstimatedTokensKey] = freedEstimatedTokens,
            },
        };

        return boundaryMessage;
    }

    private sealed record ShrinkPlan(
        IReadOnlyList<ToolResultCandidate> Candidates,
        int KeepRecentCount);

    private sealed record ToolResultCandidate(
        string CallId,
        string ToolName,
        ChatMessage Message,
        FunctionResultContent Result);
}
