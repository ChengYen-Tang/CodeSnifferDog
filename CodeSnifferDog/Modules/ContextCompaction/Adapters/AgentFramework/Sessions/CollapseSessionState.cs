using Microsoft.Agents.AI;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;

public sealed class CollapseSessionState
{
    private const string StateKey = "codesnifferdog.context_compaction.collapse_state";
    private readonly ProviderSessionState<CollapseState> _sessionState =
        new(static _ => new CollapseState(), StateKey);

    public CollapseState Get(AgentSession? session) =>
        _sessionState.GetOrInitializeState(session);

    public void Reset(AgentSession? session) =>
        _sessionState.SaveState(session, new CollapseState());

    public CollapseState StageCollapseSpan(
        AgentSession? session,
        CompactionResult result,
        CompactionReason reason)
    {
        ArgumentNullException.ThrowIfNull(result);

        StagedCollapseSpan stagedSpan = CreateStagedSpan(Get(session), result, reason);
        CollapseState currentState = Get(session);
        CollapseState nextState = CloneState(
            currentState,
            stagedSpans: [.. currentState.StagedSpans, stagedSpan],
            snapshot: CloneSnapshot(currentState.Snapshot, lastStagedCollapseId: stagedSpan.CollapseId));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    public CollapseState CommitStagedSpan(
        AgentSession? session,
        string collapseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collapseId);

        CollapseState currentState = Get(session);
        StagedCollapseSpan? stagedSpan = currentState.StagedSpans
            .FirstOrDefault(span => string.Equals(span.CollapseId, collapseId, StringComparison.Ordinal));

        if (stagedSpan is null)
            return currentState;

        CommittedCollapseSpan committedSpan = new()
        {
            CollapseId = stagedSpan.CollapseId,
            SummaryMessageId = stagedSpan.SummaryMessageId,
            ProjectionMessageId = stagedSpan.ProjectionMessageId,
            ContinuityProjectionMessageId = stagedSpan.ContinuityProjectionMessageId,
            Summary = stagedSpan.Summary,
            ContinuityState = stagedSpan.ContinuityState,
            Reason = stagedSpan.Reason,
            FirstArchivedMessageIndex = stagedSpan.FirstArchivedMessageIndex,
            FirstArchivedMessageId = stagedSpan.FirstArchivedMessageId,
            FirstArchivedMessageRole = stagedSpan.FirstArchivedMessageRole,
            FirstArchivedMessageText = stagedSpan.FirstArchivedMessageText,
            LastArchivedMessageIndex = stagedSpan.LastArchivedMessageIndex,
            LastArchivedMessageId = stagedSpan.LastArchivedMessageId,
            LastArchivedMessageRole = stagedSpan.LastArchivedMessageRole,
            LastArchivedMessageText = stagedSpan.LastArchivedMessageText,
            ArchivedMessagesCount = stagedSpan.ArchivedMessagesCount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        IReadOnlyList<StagedCollapseSpan> remainingStagedSpans =
            [.. currentState.StagedSpans.Where(span => !string.Equals(span.CollapseId, collapseId, StringComparison.Ordinal))];
        CollapseState nextState = CloneState(
            currentState,
            stagedSpans: remainingStagedSpans,
            lastCollapseReason: stagedSpan.Reason,
            commits: [.. currentState.Commits, committedSpan],
            snapshot: CloneSnapshot(
                currentState.Snapshot,
                lastCommittedCollapseId: committedSpan.CollapseId,
                lastStagedCollapseId: remainingStagedSpans.Count > 0 ? remainingStagedSpans[^1].CollapseId : null,
                preserveLastStagedCollapseId: false,
                armed: remainingStagedSpans.Count > 0,
                preserveArmed: false));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    public CollapseState DiscardStagedSpan(
        AgentSession? session,
        string collapseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collapseId);

        CollapseState currentState = Get(session);
        IReadOnlyList<StagedCollapseSpan> remainingStagedSpans =
            [.. currentState.StagedSpans.Where(span => !string.Equals(span.CollapseId, collapseId, StringComparison.Ordinal))];
        CollapseState nextState = CloneState(
            currentState,
            stagedSpans: remainingStagedSpans,
            snapshot: CloneSnapshot(
                currentState.Snapshot,
                lastStagedCollapseId: remainingStagedSpans.Count > 0 ? remainingStagedSpans[^1].CollapseId : null,
                preserveLastStagedCollapseId: false,
                armed: remainingStagedSpans.Count > 0,
                preserveArmed: false,
                lastArmedAtUtc: remainingStagedSpans.Count > 0 ? currentState.Snapshot.LastArmedAtUtc : null,
                preserveLastArmedAtUtc: remainingStagedSpans.Count > 0));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    public CollapseState RecordProjection(
        AgentSession? session,
        IReadOnlyList<string> projectedCollapseIds)
    {
        ArgumentNullException.ThrowIfNull(projectedCollapseIds);

        CollapseState currentState = Get(session);
        CollapseState nextState = CloneState(
            currentState,
            snapshot: CloneSnapshot(
                currentState.Snapshot,
                projectedCollapseIds: [.. projectedCollapseIds],
                lastProjectedAtUtc: projectedCollapseIds.Count > 0 ? DateTimeOffset.UtcNow : currentState.Snapshot.LastProjectedAtUtc));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    public CollapseState RecordSpawnObservation(
        AgentSession? session,
        int estimatedTokens,
        bool armed)
    {
        CollapseState currentState = Get(session);
        CollapseState nextState = CloneState(
            currentState,
            snapshot: CloneSnapshot(
                currentState.Snapshot,
                lastSpawnTokens: estimatedTokens,
                preserveLastSpawnTokens: false,
                armed: armed,
                preserveArmed: false,
                lastArmedAtUtc: armed ? DateTimeOffset.UtcNow : null,
                preserveLastArmedAtUtc: armed));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    private static StagedCollapseSpan CreateStagedSpan(
        CollapseState currentState,
        CompactionResult result,
        CompactionReason reason)
    {
        CompactionMessageReference? firstArchivedReference =
            result.ArchivedMessageReferences.Count > 0 ? result.ArchivedMessageReferences[0] : null;
        CompactionMessageReference? lastArchivedReference =
            result.ArchivedMessageReferences.Count > 0 ? result.ArchivedMessageReferences[^1] : null;
        if (firstArchivedReference is null || lastArchivedReference is null)
            throw new InvalidOperationException("Context collapse requires at least one archived message reference.");

        string summary = result.BoundaryMessage.AdditionalProperties?
            .GetValueOrDefault(CompactionArtifactMetadata.BoundarySummaryKey)?
            .ToString() ?? string.Empty;

        string collapseId = GetNextCollapseId(currentState);

        return new StagedCollapseSpan
        {
            CollapseId = collapseId,
            SummaryMessageId = CreateSummaryMessageId(collapseId),
            ProjectionMessageId = CreateProjectionMessageId(collapseId),
            ContinuityProjectionMessageId = CreateContinuityProjectionMessageId(collapseId),
            Summary = summary,
            ContinuityState = result.ContinuityState,
            Reason = reason.ToString(),
            FirstArchivedMessageIndex = firstArchivedReference.MessageIndex,
            FirstArchivedMessageId = firstArchivedReference.MessageId,
            FirstArchivedMessageRole = firstArchivedReference.Role.ToString(),
            FirstArchivedMessageText = firstArchivedReference.Text,
            LastArchivedMessageIndex = lastArchivedReference.MessageIndex,
            LastArchivedMessageId = lastArchivedReference.MessageId,
            LastArchivedMessageRole = lastArchivedReference.Role.ToString(),
            LastArchivedMessageText = lastArchivedReference.Text,
            ArchivedMessagesCount = result.ArchivedMessageReferences.Count,
            StagedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static string GetNextCollapseId(CollapseState currentState)
    {
        int nextId = currentState.Commits
            .Select(static span => TryParseCollapseId(span.CollapseId))
            .Concat(currentState.StagedSpans.Select(static span => TryParseCollapseId(span.CollapseId)))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return nextId.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int TryParseCollapseId(string collapseId) =>
        int.TryParse(collapseId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;

    private static string CreateSummaryMessageId(string collapseId) => $"collapse-summary-{collapseId}";

    private static string CreateProjectionMessageId(string collapseId) => $"collapse-projection-{collapseId}";

    private static string CreateContinuityProjectionMessageId(string collapseId) => $"collapse-continuity-{collapseId}";

    private static CollapseState CloneState(
        CollapseState currentState,
        IReadOnlyList<StagedCollapseSpan>? stagedSpans = null,
        string? lastCollapseReason = null,
        bool preserveLastCollapseReason = true,
        IReadOnlyList<CommittedCollapseSpan>? commits = null,
        CollapseSnapshot? snapshot = null) => new()
        {
            StagedSpans = stagedSpans ?? currentState.StagedSpans,
            LastCollapseReason = preserveLastCollapseReason ? lastCollapseReason ?? currentState.LastCollapseReason : lastCollapseReason,
            Commits = commits ?? currentState.Commits,
            Snapshot = snapshot ?? currentState.Snapshot,
        };

    private static CollapseSnapshot CloneSnapshot(
        CollapseSnapshot currentSnapshot,
        IReadOnlyList<string>? projectedCollapseIds = null,
        string? lastCommittedCollapseId = null,
        bool preserveLastCommittedCollapseId = true,
        string? lastStagedCollapseId = null,
        bool preserveLastStagedCollapseId = true,
        DateTimeOffset? lastProjectedAtUtc = null,
        bool preserveLastProjectedAtUtc = true,
        bool armed = false,
        bool preserveArmed = true,
        int? lastSpawnTokens = null,
        bool preserveLastSpawnTokens = true,
        DateTimeOffset? lastArmedAtUtc = null,
        bool preserveLastArmedAtUtc = true) => new()
        {
            ProjectedCollapseIds = projectedCollapseIds ?? currentSnapshot.ProjectedCollapseIds,
            LastCommittedCollapseId = preserveLastCommittedCollapseId ? lastCommittedCollapseId ?? currentSnapshot.LastCommittedCollapseId : lastCommittedCollapseId,
            LastStagedCollapseId = preserveLastStagedCollapseId ? lastStagedCollapseId ?? currentSnapshot.LastStagedCollapseId : lastStagedCollapseId,
            LastProjectedAtUtc = preserveLastProjectedAtUtc ? lastProjectedAtUtc ?? currentSnapshot.LastProjectedAtUtc : lastProjectedAtUtc,
            Armed = preserveArmed ? armed || currentSnapshot.Armed : armed,
            LastSpawnTokens = preserveLastSpawnTokens ? lastSpawnTokens ?? currentSnapshot.LastSpawnTokens : lastSpawnTokens,
            LastArmedAtUtc = preserveLastArmedAtUtc ? lastArmedAtUtc ?? currentSnapshot.LastArmedAtUtc : lastArmedAtUtc,
        };
}
