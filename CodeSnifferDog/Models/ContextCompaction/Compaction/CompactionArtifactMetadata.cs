
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
    public const string BoundaryAnchorIndexKey = "codesnifferdog.compaction.boundary_anchor_index";
    public const string BoundaryAnchorIdKey = "codesnifferdog.compaction.boundary_anchor_id";
    public const string BoundaryAnchorRoleKey = "codesnifferdog.compaction.boundary_anchor_role";
    public const string BoundaryAnchorTextKey = "codesnifferdog.compaction.boundary_anchor_text";
    public const string BoundarySummaryKey = "codesnifferdog.compaction.boundary_summary";
    public const string PreservedSegmentHeadIndexKey = "codesnifferdog.compaction.preserved_segment_head_index";
    public const string PreservedSegmentHeadIdKey = "codesnifferdog.compaction.preserved_segment_head_id";
    public const string PreservedSegmentTailIndexKey = "codesnifferdog.compaction.preserved_segment_tail_index";
    public const string PreservedSegmentTailIdKey = "codesnifferdog.compaction.preserved_segment_tail_id";
    public const string PreservedTailIndexesKey = "codesnifferdog.compaction.preserved_tail_indexes";
    public const string PreservedTailIdsKey = "codesnifferdog.compaction.preserved_tail_ids";
    public const string PreservedTailTextsKey = "codesnifferdog.compaction.preserved_tail_texts";
    public const string MessagesToKeepCountKey = "codesnifferdog.compaction.messages_to_keep_count";
    public const string AttachmentsCountKey = "codesnifferdog.compaction.attachments_count";
    public const string HookResultsCountKey = "codesnifferdog.compaction.hook_results_count";
    public const string ContinuityCurrentObjectiveKey = "codesnifferdog.compaction.continuity.current_objective";
    public const string ContinuityCompletedWorkKey = "codesnifferdog.compaction.continuity.completed_work";
    public const string ContinuityNextStepsKey = "codesnifferdog.compaction.continuity.next_steps";
    public const string ContinuityCriticalContextKey = "codesnifferdog.compaction.continuity.critical_context";
    public const string ShrinkOperationKey = "codesnifferdog.compaction.shrink_operation";
    public const string ShrunkToolResultCountKey = "codesnifferdog.compaction.shrunk_tool_result_count";
    public const string FreedEstimatedTokensKey = "codesnifferdog.compaction.freed_estimated_tokens";
    public const string CompactedToolCallIdKey = "codesnifferdog.compaction.compacted_tool_call_id";
    public const string CompactedToolNameKey = "codesnifferdog.compaction.compacted_tool_name";
    public const string CompactedToolResultKindKey = "codesnifferdog.compaction.compacted_tool_result_kind";
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
