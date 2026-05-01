using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Agents.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class OperationalContextCollapseSessionState
{
    private const string StateKey = "codesnifferdog.context_compaction.collapse_state";
    private readonly ProviderSessionState<OperationalContextCollapseState> _sessionState =
        new(static _ => new OperationalContextCollapseState(), StateKey);

    public OperationalContextCollapseState Get(AgentSession? session) =>
        _sessionState.GetOrInitializeState(session);

    public void Reset(AgentSession? session) =>
        _sessionState.SaveState(session, new OperationalContextCollapseState());

    public OperationalContextCollapseState StageCollapseSpan(
        AgentSession? session,
        OperationalContextCompactionResult result,
        OperationalContextCompactionReason reason)
    {
        ArgumentNullException.ThrowIfNull(result);

        OperationalContextStagedCollapseSpan stagedSpan = CreateStagedSpan(Get(session), result, reason);
        OperationalContextCollapseState currentState = Get(session);
        OperationalContextCollapseState nextState = CloneState(
            currentState,
            stagedSpans: [.. currentState.StagedSpans, stagedSpan],
            snapshot: CloneSnapshot(currentState.Snapshot, lastStagedCollapseId: stagedSpan.CollapseId));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    public OperationalContextCollapseState CommitStagedSpan(
        AgentSession? session,
        string collapseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collapseId);

        OperationalContextCollapseState currentState = Get(session);
        OperationalContextStagedCollapseSpan? stagedSpan = currentState.StagedSpans
            .FirstOrDefault(span => string.Equals(span.CollapseId, collapseId, StringComparison.Ordinal));

        if (stagedSpan is null)
            return currentState;

        OperationalContextCommittedCollapseSpan committedSpan = new()
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

        IReadOnlyList<OperationalContextStagedCollapseSpan> remainingStagedSpans =
            [.. currentState.StagedSpans.Where(span => !string.Equals(span.CollapseId, collapseId, StringComparison.Ordinal))];
        OperationalContextCollapseState nextState = CloneState(
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

    public OperationalContextCollapseState DiscardStagedSpan(
        AgentSession? session,
        string collapseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collapseId);

        OperationalContextCollapseState currentState = Get(session);
        IReadOnlyList<OperationalContextStagedCollapseSpan> remainingStagedSpans =
            [.. currentState.StagedSpans.Where(span => !string.Equals(span.CollapseId, collapseId, StringComparison.Ordinal))];
        OperationalContextCollapseState nextState = CloneState(
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

    public OperationalContextCollapseState RecordProjection(
        AgentSession? session,
        IReadOnlyList<string> projectedCollapseIds)
    {
        ArgumentNullException.ThrowIfNull(projectedCollapseIds);

        OperationalContextCollapseState currentState = Get(session);
        OperationalContextCollapseState nextState = CloneState(
            currentState,
            snapshot: CloneSnapshot(
                currentState.Snapshot,
                projectedCollapseIds: [.. projectedCollapseIds],
                lastProjectedAtUtc: projectedCollapseIds.Count > 0 ? DateTimeOffset.UtcNow : currentState.Snapshot.LastProjectedAtUtc));

        _sessionState.SaveState(session, nextState);
        return nextState;
    }

    public OperationalContextCollapseState RecordSpawnObservation(
        AgentSession? session,
        int estimatedTokens,
        bool armed)
    {
        OperationalContextCollapseState currentState = Get(session);
        OperationalContextCollapseState nextState = CloneState(
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

    private static OperationalContextStagedCollapseSpan CreateStagedSpan(
        OperationalContextCollapseState currentState,
        OperationalContextCompactionResult result,
        OperationalContextCompactionReason reason)
    {
        OperationalContextCompactionMessageReference? firstArchivedReference =
            result.ArchivedMessageReferences.Count > 0 ? result.ArchivedMessageReferences[0] : null;
        OperationalContextCompactionMessageReference? lastArchivedReference =
            result.ArchivedMessageReferences.Count > 0 ? result.ArchivedMessageReferences[^1] : null;
        if (firstArchivedReference is null || lastArchivedReference is null)
            throw new InvalidOperationException("Context collapse requires at least one archived message reference.");

        string summary = result.BoundaryMessage.AdditionalProperties?
            .GetValueOrDefault(OperationalContextCompactionArtifactMetadata.BoundarySummaryKey)?
            .ToString() ?? string.Empty;

        string collapseId = GetNextCollapseId(currentState);

        return new OperationalContextStagedCollapseSpan
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

    private static string GetNextCollapseId(OperationalContextCollapseState currentState)
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

    private static OperationalContextCollapseState CloneState(
        OperationalContextCollapseState currentState,
        IReadOnlyList<OperationalContextStagedCollapseSpan>? stagedSpans = null,
        string? lastCollapseReason = null,
        bool preserveLastCollapseReason = true,
        IReadOnlyList<OperationalContextCommittedCollapseSpan>? commits = null,
        OperationalContextCollapseSnapshot? snapshot = null) => new()
        {
            StagedSpans = stagedSpans ?? currentState.StagedSpans,
            LastCollapseReason = preserveLastCollapseReason ? lastCollapseReason ?? currentState.LastCollapseReason : lastCollapseReason,
            Commits = commits ?? currentState.Commits,
            Snapshot = snapshot ?? currentState.Snapshot,
        };

    private static OperationalContextCollapseSnapshot CloneSnapshot(
        OperationalContextCollapseSnapshot currentSnapshot,
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
