namespace CodeSnifferDog.Models.ContextCompaction;

public static class OperationalContextCompactionArtifactMetadata
{
    public const string ArtifactKindKey = "codesnifferdog.compaction.artifact_kind";
    public const string MessageIdentityKey = "codesnifferdog.compaction.message_id";
    public const string CompactionReasonKey = "codesnifferdog.compaction.reason";
    public const string SummaryFormatVersionKey = "codesnifferdog.compaction.summary_format_version";
    public const string IsCompactionSummaryKey = "codesnifferdog.compaction.is_summary";
    public const string HasPreservedTailKey = "codesnifferdog.compaction.has_preserved_tail";
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
    public const string BoundaryArtifactKind = "boundary";
    public const string SummaryArtifactKind = "summary";
    public const string AttachmentArtifactKind = "attachment";
    public const string HookResultArtifactKind = "hook_result";
    public const string ContinuityArtifactKind = "continuity_state";
    public const string SnipBoundaryArtifactKind = "snip_boundary";
    public const string CollapseProjectionArtifactKind = "collapse_projection";
    public const string MicroCompactToolResultArtifactKind = "microcompact_tool_result";
    public const int CurrentSummaryFormatVersion = 1;
}
