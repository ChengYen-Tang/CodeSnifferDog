using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Projects committed collapses into outgoing requests and manages staged collapses around proactive or reactive retries.
/// </summary>
/// <param name="reducer">Reducer used to create new collapse spans when thresholds are reached.</param>
/// <param name="projectionBuilder">Optional projection builder dependency retained for compatibility.</param>
/// <param name="sessionState">Session-scoped collapse state store.</param>
public sealed class CollapseController(
    ChatReducer reducer,
    CollapseProjectionBuilder? projectionBuilder = null,
    CollapseSessionState? sessionState = null)
{
    private readonly CollapseProjectionBuilder _projectionBuilder =
        projectionBuilder ?? new CollapseProjectionBuilder();
    private readonly ChatReducer _reducer =
        reducer ?? throw new ArgumentNullException(nameof(reducer));
    private readonly CollapseSessionState _sessionState =
        sessionState ?? new CollapseSessionState();

    /// <summary>
    /// Applies committed collapse projections to the request transcript for the supplied session.
    /// </summary>
    /// <param name="requestMessages">Original request messages before collapse projection.</param>
    /// <param name="session">Agent session whose committed collapse spans should be projected.</param>
    /// <returns>The request messages with committed collapse projections substituted in.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requestMessages" /> is <see langword="null" />.</exception>
    public IReadOnlyList<ChatMessage> PrepareMessages(
        IReadOnlyList<ChatMessage> requestMessages,
        AgentSession? session)
    {
        ArgumentNullException.ThrowIfNull(requestMessages);

        (IReadOnlyList<ChatMessage> messages, IReadOnlyList<string> projectedCollapseIds) = CollapseProjectionBuilder.BuildProjection(
            requestMessages,
            _sessionState.Get(session),
            _reducer.Options);
        _sessionState.RecordProjection(session, projectedCollapseIds);

        return messages;
    }

    /// <summary>
    /// Attempts a proactive collapse when the projected transcript crosses the proactive threshold.
    /// </summary>
    /// <param name="requestMessages">Original request messages before projection.</param>
    /// <param name="session">Agent session whose collapse state should be updated.</param>
    /// <param name="cancellationToken">Cancels the proactive collapse attempt.</param>
    /// <returns>The projected transcript, or a reprojected transcript if a staged collapse was committed immediately.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requestMessages" /> is <see langword="null" />.</exception>
    public async ValueTask<IReadOnlyList<ChatMessage>> TryPrepareProactiveCollapseAsync(
        IReadOnlyList<ChatMessage> requestMessages,
        AgentSession? session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestMessages);

        IReadOnlyList<ChatMessage> committedProjectionMessages = PrepareMessages(requestMessages, session);
        int estimatedTokens = TokenEstimator.Estimate(committedProjectionMessages);
        bool shouldArm = estimatedTokens >= _reducer.Options.GetCollapseProactiveThreshold();
        _sessionState.RecordSpawnObservation(session, estimatedTokens, shouldArm);

        if (!shouldArm)
            return committedProjectionMessages;

        CompactionResult result =
            await _reducer.CompactReactiveAsync(committedProjectionMessages, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> pendingCollapseIds = StagePendingCollapse(
            committedProjectionMessages,
            session,
            result,
            CompactionReason.ContextCollapseProactive);

        if (estimatedTokens < _reducer.Options.GetCollapseBlockingThreshold() ||
            pendingCollapseIds.Count == 0)
            return committedProjectionMessages;

        CommitPendingCollapses(session, pendingCollapseIds);
        return PrepareMessages(requestMessages, session);
    }

    /// <summary>
    /// Creates a reactive collapse for a retry path and commits any newly staged span before reprojection.
    /// </summary>
    /// <param name="originalMessages">Original request messages before projection.</param>
    /// <param name="session">Agent session whose collapse state should be updated.</param>
    /// <param name="cancellationToken">Cancels the reactive collapse attempt.</param>
    /// <returns>The projected transcript that should be used for the retry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="originalMessages" /> is <see langword="null" />.</exception>
    public async ValueTask<IReadOnlyList<ChatMessage>> PrepareReactiveRetryAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalMessages);

        IReadOnlyList<ChatMessage> committedProjectionMessages = PrepareMessages(originalMessages, session);
        CompactionResult result =
            await _reducer.CompactReactiveAsync(committedProjectionMessages, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> pendingCollapseIds = StagePendingCollapse(
            committedProjectionMessages,
            session,
            result,
            CompactionReason.Reactive);

        if (pendingCollapseIds.Count == 0)
            return committedProjectionMessages;

        CommitPendingCollapses(session, pendingCollapseIds);
        return PrepareMessages(originalMessages, session);
    }

    /// <summary>
    /// Captures the identifiers of all currently staged collapse spans for the supplied session.
    /// </summary>
    /// <param name="session">Agent session whose staged collapse identifiers should be captured.</param>
    /// <returns>The set of staged collapse identifiers.</returns>
    public HashSet<string> CaptureStagedCollapseIds(AgentSession? session) =>
        [.. _sessionState.Get(session).StagedSpans.Select(static span => span.CollapseId)];

    /// <summary>
    /// Computes the staged collapse identifiers that were added after an earlier snapshot.
    /// </summary>
    /// <param name="session">Agent session whose staged collapse identifiers should be compared.</param>
    /// <param name="initialStagedCollapseIds">Previously captured staged collapse identifiers.</param>
    /// <returns>The set difference representing newly staged collapses.</returns>
    public HashSet<string> CaptureNewStagedCollapseIds(
        AgentSession? session,
        HashSet<string> initialStagedCollapseIds) =>
        [.. _sessionState
            .Get(session)
            .StagedSpans
            .Select(static span => span.CollapseId)
            .Where(collapseId => !initialStagedCollapseIds.Contains(collapseId))];

    /// <summary>
    /// Promotes the supplied staged collapse identifiers into committed spans.
    /// </summary>
    /// <param name="session">Agent session whose staged spans should be committed.</param>
    /// <param name="collapseIds">Identifiers of the staged spans to commit.</param>
    public void CommitPendingCollapses(
        AgentSession? session,
        IEnumerable<string> collapseIds)
    {
        foreach (string collapseId in collapseIds)
            _sessionState.CommitStagedSpan(session, collapseId);
    }

    /// <summary>
    /// Discards the supplied staged collapse identifiers without committing them.
    /// </summary>
    /// <param name="session">Agent session whose staged spans should be discarded.</param>
    /// <param name="collapseIds">Identifiers of the staged spans to discard.</param>
    public void DiscardPendingCollapses(
        AgentSession? session,
        IEnumerable<string> collapseIds)
    {
        foreach (string collapseId in collapseIds)
            _sessionState.DiscardStagedSpan(session, collapseId);
    }

    private IReadOnlyList<string> StagePendingCollapse(
        IReadOnlyList<ChatMessage> _,
        AgentSession? session,
        CompactionResult result,
        CompactionReason reason)
    {
        if (!result.WasCompacted || result.ArchivedMessageReferences.Count == 0)
            return [];

        CollapseState state = _sessionState.StageCollapseSpan(
            session,
            result,
            reason);
        string? pendingCollapseId = state.Snapshot.LastStagedCollapseId;

        return string.IsNullOrWhiteSpace(pendingCollapseId) ? [] : [pendingCollapseId];
    }
}
