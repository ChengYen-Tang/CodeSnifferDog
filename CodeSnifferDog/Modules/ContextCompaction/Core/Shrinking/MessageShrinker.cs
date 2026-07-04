using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Shrinking;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Rewrites verbose tool-call transcripts into smaller local artifacts before full transcript compaction is required.
/// </summary>
public sealed class MessageShrinker
{
    /// <summary>
    /// Replaces older tool result payloads with compact placeholder artifacts while keeping the surrounding messages.
    /// </summary>
    /// <param name="messages">Messages to inspect for eligible tool results.</param>
    /// <param name="options">Compaction settings that control trigger counts and eligible tool names.</param>
    /// <returns>The rewritten message sequence and shrink statistics, or a no-change result when shrinking is disabled or unnecessary.</returns>
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

    /// <summary>
    /// Removes older tool call and tool result payloads entirely and inserts a snip boundary artifact.
    /// </summary>
    /// <param name="messages">Messages to inspect for eligible tool results.</param>
    /// <param name="options">Compaction settings that control trigger counts and eligible tool names.</param>
    /// <returns>The rewritten message sequence and shrink statistics, or a no-change result when snipping is disabled or unnecessary.</returns>
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

    /// <summary>
    /// Finds shrink candidates and normalizes the trigger thresholds for one shrink mode.
    /// </summary>
    /// <param name="messages">Messages to scan for function calls and function results.</param>
    /// <param name="options">Compaction settings that define which tools are compactable.</param>
    /// <param name="triggerCount">Minimum number of eligible tool results required before shrinking begins.</param>
    /// <param name="keepRecentCount">Number of most recent tool results that must remain untouched.</param>
    /// <returns>A shrink plan describing the eligible candidates and normalized retention count.</returns>
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

    /// <summary>
    /// Rewrites messages according to the selected shrink mode and records the estimated token savings.
    /// </summary>
    /// <param name="messages">Original message sequence.</param>
    /// <param name="candidates">Tool result candidates selected for rewriting.</param>
    /// <param name="removeCompactedMessages"><see langword="true" /> to remove compacted tool payloads entirely; otherwise only rewrite results.</param>
    /// <param name="shrinkOperation">Operation name recorded in the rewritten message metadata.</param>
    /// <returns>The rewritten messages together with aggregate shrink statistics.</returns>
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

    /// <summary>
    /// Clones one message with rewritten contents and annotates it with shrink metadata.
    /// </summary>
    /// <param name="original">Original message being rewritten.</param>
    /// <param name="contents">Replacement content payloads.</param>
    /// <param name="shrinkOperation">Operation name stored in message metadata.</param>
    /// <param name="shrunkToolResultCount">Number of tool results shrunk so far in the current rewrite pass.</param>
    /// <param name="freedEstimatedTokens">Estimated tokens freed so far in the current rewrite pass.</param>
    /// <returns>A cloned message that preserves role and metadata while carrying the rewritten contents.</returns>
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

    /// <summary>
    /// Creates the placeholder text used for micro-compacted tool results.
    /// </summary>
    /// <param name="toolName">Name of the tool whose output was removed.</param>
    /// <param name="callId">Call identifier of the removed tool result.</param>
    /// <returns>A stable placeholder artifact that explains why the raw tool output was omitted.</returns>
    private static string CreateMicroCompactedToolResultArtifact(string toolName, string callId) =>
        $$"""
        [Compacted tool result]
        Tool: {{toolName}}
        CallId: {{callId}}
        Reason: framework-level-microcompact
        Omitted: raw tool output removed to reduce context pressure
        """;

    /// <summary>
    /// Creates the system boundary message that marks a snip operation.
    /// </summary>
    /// <param name="shrunkToolResultCount">Number of tool results removed by the snip.</param>
    /// <param name="freedEstimatedTokens">Estimated tokens freed by the snip.</param>
    /// <returns>A system message that records the snip metadata.</returns>
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

    /// <summary>
    /// Describes one tool result that can be rewritten or removed during shrinking.
    /// </summary>
    /// <param name="CallId">Call identifier shared by the tool invocation and result.</param>
    /// <param name="ToolName">Tool name associated with the result.</param>
    /// <param name="Message">Original message containing the result payload.</param>
    /// <param name="Result">Function result content selected for shrinking.</param>
    private sealed record ToolResultCandidate(
        string CallId,
        string ToolName,
        ChatMessage Message,
        FunctionResultContent Result);
}
