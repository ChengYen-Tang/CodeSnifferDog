namespace CodeSnifferDog.Models.ContextCompaction;

public static class OperationalContextCompactionArtifactMetadata
{
    public const string ArtifactKindKey = "codesnifferdog.compaction.artifact_kind";
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
    public const string BoundaryArtifactKind = "boundary";
    public const string SummaryArtifactKind = "summary";
    public const int CurrentSummaryFormatVersion = 1;
}
