using Microsoft.Agents.AI;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;

/// <summary>
/// Persists per-session collapse state, including staged spans, committed spans, and snapshot telemetry.
/// </summary>
public sealed class CollapseSessionState
{
    private const string StateKey = "codesnifferdog.context_compaction.collapse_state";
    private readonly ProviderSessionState<CollapseState> _sessionState =
        new(static _ => new CollapseState(), StateKey);

    /// <summary>
    /// Gets the current collapse state for the supplied session, creating an empty state on first access.
    /// </summary>
    /// <param name="session">Session whose collapse state should be loaded.</param>
    /// <returns>The current per-session collapse state.</returns>
    public CollapseState Get(AgentSession? session) =>
        _sessionState.GetOrInitializeState(session);

    /// <summary>
    /// Resets the collapse state for the supplied session to an empty value.
    /// </summary>
    /// <param name="session">Session whose collapse state should be cleared.</param>
    public void Reset(AgentSession? session) =>
        _sessionState.SaveState(session, new CollapseState());

    /// <summary>
    /// Converts one compaction result into a staged collapse span that can be committed or discarded later.
    /// </summary>
    /// <param name="session">Session whose collapse state should be updated.</param>
    /// <param name="result">Compaction result that produced archived message references.</param>
    /// <param name="reason">Reason the collapse span is being staged.</param>
    /// <returns>The updated collapse state containing the new staged span.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="result" /> does not contain archived message references.</exception>
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

    /// <summary>
    /// Promotes one staged collapse span into the committed span list.
    /// </summary>
    /// <param name="session">Session whose collapse state should be updated.</param>
    /// <param name="collapseId">Identifier of the staged collapse span to commit.</param>
    /// <returns>The updated collapse state, or the unchanged state when the identifier is not staged.</returns>
    /// <exception cref="ArgumentException"><paramref name="collapseId" /> is <see langword="null" />, empty, or whitespace.</exception>
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

    /// <summary>
    /// Removes one staged collapse span without committing it.
    /// </summary>
    /// <param name="session">Session whose collapse state should be updated.</param>
    /// <param name="collapseId">Identifier of the staged collapse span to discard.</param>
    /// <returns>The updated collapse state after the staged span is removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="collapseId" /> is <see langword="null" />, empty, or whitespace.</exception>
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

    /// <summary>
    /// Records which committed collapse identifiers were projected into the current request.
    /// </summary>
    /// <param name="session">Session whose projection snapshot should be updated.</param>
    /// <param name="projectedCollapseIds">Collapse identifiers projected into the request transcript.</param>
    /// <returns>The updated collapse state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projectedCollapseIds" /> is <see langword="null" />.</exception>
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

    /// <summary>
    /// Records a proactive-collapse threshold observation for the current session.
    /// </summary>
    /// <param name="session">Session whose snapshot telemetry should be updated.</param>
    /// <param name="estimatedTokens">Estimated token count observed for the projected request.</param>
    /// <param name="armed"><see langword="true" /> when the proactive-collapse threshold is currently armed.</param>
    /// <returns>The updated collapse state.</returns>
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

    /// <summary>
    /// Creates the staged span persisted after a compaction pass archives part of the transcript.
    /// </summary>
    /// <param name="currentState">Current collapse state used to generate the next collapse identifier.</param>
    /// <param name="result">Compaction result that produced the archived message references.</param>
    /// <param name="reason">Reason the collapse span is being staged.</param>
    /// <returns>The staged collapse span that can later be committed or discarded.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="result" /> does not contain at least one archived message reference.</exception>
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

    /// <summary>
    /// Allocates the next monotonic collapse identifier across committed and staged spans.
    /// </summary>
    /// <param name="currentState">Current collapse state whose identifiers must remain unique.</param>
    /// <returns>The next zero-padded collapse identifier.</returns>
    private static string GetNextCollapseId(CollapseState currentState)
    {
        int nextId = currentState.Commits
            .Select(static span => TryParseCollapseId(span.CollapseId))
            .Concat(currentState.StagedSpans.Select(static span => TryParseCollapseId(span.CollapseId)))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return nextId.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a numeric collapse identifier, returning zero when the identifier is malformed.
    /// </summary>
    /// <param name="collapseId">Collapse identifier to parse.</param>
    /// <returns>The parsed numeric identifier, or zero when parsing fails.</returns>
    private static int TryParseCollapseId(string collapseId) =>
        int.TryParse(collapseId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;

    /// <summary>
    /// Creates the message identity for the committed summary artifact of one collapse span.
    /// </summary>
    /// <param name="collapseId">Collapse identifier that owns the summary artifact.</param>
    /// <returns>The summary artifact message identity.</returns>
    private static string CreateSummaryMessageId(string collapseId) => $"collapse-summary-{collapseId}";

    /// <summary>
    /// Creates the message identity for the projection artifact of one collapse span.
    /// </summary>
    /// <param name="collapseId">Collapse identifier that owns the projection artifact.</param>
    /// <returns>The projection artifact message identity.</returns>
    private static string CreateProjectionMessageId(string collapseId) => $"collapse-projection-{collapseId}";

    /// <summary>
    /// Creates the message identity for the projected continuity artifact of one collapse span.
    /// </summary>
    /// <param name="collapseId">Collapse identifier that owns the continuity projection artifact.</param>
    /// <returns>The continuity projection artifact message identity.</returns>
    private static string CreateContinuityProjectionMessageId(string collapseId) => $"collapse-continuity-{collapseId}";

    /// <summary>
    /// Clones collapse state while selectively replacing mutable slices and snapshot values.
    /// </summary>
    /// <param name="currentState">Current state to clone.</param>
    /// <param name="stagedSpans">Optional replacement staged spans.</param>
    /// <param name="lastCollapseReason">Optional replacement last-collapse reason.</param>
    /// <param name="preserveLastCollapseReason"><see langword="true" /> to fall back to the current state's last-collapse reason when no replacement is supplied.</param>
    /// <param name="commits">Optional replacement committed spans.</param>
    /// <param name="snapshot">Optional replacement snapshot.</param>
    /// <returns>The cloned collapse state.</returns>
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

    /// <summary>
    /// Clones collapse snapshot telemetry while allowing specific fields to be replaced or intentionally cleared.
    /// </summary>
    /// <param name="currentSnapshot">Current snapshot to clone.</param>
    /// <param name="projectedCollapseIds">Optional replacement projected collapse identifiers.</param>
    /// <param name="lastCommittedCollapseId">Optional replacement last committed collapse identifier.</param>
    /// <param name="preserveLastCommittedCollapseId"><see langword="true" /> to retain the existing committed identifier when no replacement is supplied.</param>
    /// <param name="lastStagedCollapseId">Optional replacement last staged collapse identifier.</param>
    /// <param name="preserveLastStagedCollapseId"><see langword="true" /> to retain the existing staged identifier when no replacement is supplied.</param>
    /// <param name="lastProjectedAtUtc">Optional replacement projection timestamp.</param>
    /// <param name="preserveLastProjectedAtUtc"><see langword="true" /> to retain the existing projection timestamp when no replacement is supplied.</param>
    /// <param name="armed">Optional replacement armed flag.</param>
    /// <param name="preserveArmed"><see langword="true" /> to merge the new armed flag with the existing armed flag.</param>
    /// <param name="lastSpawnTokens">Optional replacement observed spawn token count.</param>
    /// <param name="preserveLastSpawnTokens"><see langword="true" /> to retain the existing spawn token count when no replacement is supplied.</param>
    /// <param name="lastArmedAtUtc">Optional replacement last-armed timestamp.</param>
    /// <param name="preserveLastArmedAtUtc"><see langword="true" /> to retain the existing last-armed timestamp when no replacement is supplied.</param>
    /// <returns>The cloned snapshot.</returns>
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
