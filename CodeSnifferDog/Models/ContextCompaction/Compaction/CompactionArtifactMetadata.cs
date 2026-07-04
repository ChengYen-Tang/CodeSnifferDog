
namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// Metadata keys and artifact-kind constants attached to synthetic messages produced during compaction.
/// </summary>
public static class CompactionArtifactMetadata
{
    /// <summary>
    /// Metadata key that stores the artifact kind.
    /// </summary>
    public const string ArtifactKindKey = "codesnifferdog.compaction.artifact_kind";
    /// <summary>
    /// Metadata key that stores the source message identifier.
    /// </summary>
    public const string MessageIdentityKey = "codesnifferdog.compaction.message_id";
    /// <summary>
    /// Metadata key that stores the compaction reason.
    /// </summary>
    public const string CompactionReasonKey = "codesnifferdog.compaction.reason";
    /// <summary>
    /// Metadata key that stores the summary format version.
    /// </summary>
    public const string SummaryFormatVersionKey = "codesnifferdog.compaction.summary_format_version";
    /// <summary>
    /// Metadata key that marks a message as a compaction summary.
    /// </summary>
    public const string IsCompactionSummaryKey = "codesnifferdog.compaction.is_summary";
    /// <summary>
    /// Metadata key that indicates whether a preserved tail exists.
    /// </summary>
    public const string HasPreservedTailKey = "codesnifferdog.compaction.has_preserved_tail";
    /// <summary>
    /// Metadata key that stores how many preserved-tail messages were kept.
    /// </summary>
    public const string PreservedTailCountKey = "codesnifferdog.compaction.preserved_tail_count";

    /// <summary>
    /// Metadata key that stores the index of the boundary anchor message.
    /// </summary>
    public const string BoundaryAnchorIndexKey = "codesnifferdog.compaction.boundary_anchor_index";

    /// <summary>
    /// Metadata key that stores the identifier of the boundary anchor message.
    /// </summary>
    public const string BoundaryAnchorIdKey = "codesnifferdog.compaction.boundary_anchor_id";

    /// <summary>
    /// Metadata key that stores the role of the boundary anchor message.
    /// </summary>
    public const string BoundaryAnchorRoleKey = "codesnifferdog.compaction.boundary_anchor_role";

    /// <summary>
    /// Metadata key that stores the text snapshot of the boundary anchor message.
    /// </summary>
    public const string BoundaryAnchorTextKey = "codesnifferdog.compaction.boundary_anchor_text";

    /// <summary>
    /// Metadata key that stores the synthesized boundary summary text.
    /// </summary>
    public const string BoundarySummaryKey = "codesnifferdog.compaction.boundary_summary";

    /// <summary>
    /// Metadata key that stores the first preserved-segment message index.
    /// </summary>
    public const string PreservedSegmentHeadIndexKey = "codesnifferdog.compaction.preserved_segment_head_index";

    /// <summary>
    /// Metadata key that stores the identifier of the first preserved-segment message.
    /// </summary>
    public const string PreservedSegmentHeadIdKey = "codesnifferdog.compaction.preserved_segment_head_id";

    /// <summary>
    /// Metadata key that stores the last preserved-segment message index.
    /// </summary>
    public const string PreservedSegmentTailIndexKey = "codesnifferdog.compaction.preserved_segment_tail_index";

    /// <summary>
    /// Metadata key that stores the identifier of the last preserved-segment message.
    /// </summary>
    public const string PreservedSegmentTailIdKey = "codesnifferdog.compaction.preserved_segment_tail_id";

    /// <summary>
    /// Metadata key that stores the preserved-tail message indexes.
    /// </summary>
    public const string PreservedTailIndexesKey = "codesnifferdog.compaction.preserved_tail_indexes";

    /// <summary>
    /// Metadata key that stores the preserved-tail message identifiers.
    /// </summary>
    public const string PreservedTailIdsKey = "codesnifferdog.compaction.preserved_tail_ids";

    /// <summary>
    /// Metadata key that stores preserved-tail text snapshots.
    /// </summary>
    public const string PreservedTailTextsKey = "codesnifferdog.compaction.preserved_tail_texts";

    /// <summary>
    /// Metadata key that stores how many transcript messages were kept after compaction.
    /// </summary>
    public const string MessagesToKeepCountKey = "codesnifferdog.compaction.messages_to_keep_count";

    /// <summary>
    /// Metadata key that stores how many attachment messages were preserved.
    /// </summary>
    public const string AttachmentsCountKey = "codesnifferdog.compaction.attachments_count";

    /// <summary>
    /// Metadata key that stores how many hook-result messages were preserved.
    /// </summary>
    public const string HookResultsCountKey = "codesnifferdog.compaction.hook_results_count";

    /// <summary>
    /// Metadata key that stores the continuity current objective.
    /// </summary>
    public const string ContinuityCurrentObjectiveKey = "codesnifferdog.compaction.continuity.current_objective";

    /// <summary>
    /// Metadata key that stores continuity completed work.
    /// </summary>
    public const string ContinuityCompletedWorkKey = "codesnifferdog.compaction.continuity.completed_work";

    /// <summary>
    /// Metadata key that stores continuity next steps.
    /// </summary>
    public const string ContinuityNextStepsKey = "codesnifferdog.compaction.continuity.next_steps";

    /// <summary>
    /// Metadata key that stores continuity critical context.
    /// </summary>
    public const string ContinuityCriticalContextKey = "codesnifferdog.compaction.continuity.critical_context";

    /// <summary>
    /// Metadata key that stores which shrink operation ran.
    /// </summary>
    public const string ShrinkOperationKey = "codesnifferdog.compaction.shrink_operation";

    /// <summary>
    /// Metadata key that stores how many tool-result messages were shrunk.
    /// </summary>
    public const string ShrunkToolResultCountKey = "codesnifferdog.compaction.shrunk_tool_result_count";

    /// <summary>
    /// Metadata key that stores the estimated tokens freed by shrinking.
    /// </summary>
    public const string FreedEstimatedTokensKey = "codesnifferdog.compaction.freed_estimated_tokens";

    /// <summary>
    /// Metadata key that stores the compacted tool call identifier.
    /// </summary>
    public const string CompactedToolCallIdKey = "codesnifferdog.compaction.compacted_tool_call_id";

    /// <summary>
    /// Metadata key that stores the compacted tool name.
    /// </summary>
    public const string CompactedToolNameKey = "codesnifferdog.compaction.compacted_tool_name";

    /// <summary>
    /// Metadata key that stores the compacted tool-result kind.
    /// </summary>
    public const string CompactedToolResultKindKey = "codesnifferdog.compaction.compacted_tool_result_kind";

    /// <summary>
    /// Metadata key that stores the committed collapse-span identifier.
    /// </summary>
    public const string CollapseCommitIdKey = "codesnifferdog.compaction.collapse_commit_id";

    /// <summary>
    /// Artifact kind used for boundary messages.
    /// </summary>
    public const string BoundaryArtifactKind = "boundary";
    /// <summary>
    /// Artifact kind used for summary messages.
    /// </summary>
    public const string SummaryArtifactKind = "summary";
    /// <summary>
    /// Artifact kind used for preserved attachment messages.
    /// </summary>
    public const string AttachmentArtifactKind = "attachment";
    /// <summary>
    /// Artifact kind used for preserved hook-result messages.
    /// </summary>
    public const string HookResultArtifactKind = "hook_result";
    /// <summary>
    /// Artifact kind used for serialized continuity-state messages.
    /// </summary>
    public const string ContinuityArtifactKind = "continuity_state";
    /// <summary>
    /// Artifact kind used for snip boundary messages.
    /// </summary>
    public const string SnipBoundaryArtifactKind = "snip_boundary";
    /// <summary>
    /// Artifact kind used for collapse projection messages.
    /// </summary>
    public const string CollapseProjectionArtifactKind = "collapse_projection";
    /// <summary>
    /// Artifact kind used for micro-compacted tool result messages.
    /// </summary>
    public const string MicroCompactToolResultArtifactKind = "microcompact_tool_result";

    /// <summary>
    /// Current format version of compaction summary messages.
    /// </summary>
    public const int CurrentSummaryFormatVersion = 1;
}
