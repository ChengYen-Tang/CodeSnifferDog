using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextCollapseController
{
    private readonly OperationalContextCollapseProjectionBuilder _projectionBuilder;
    private readonly OperationalContextChatReducer _reducer;
    private readonly OperationalContextCollapseSessionState _sessionState;

    public OperationalContextCollapseController(
        OperationalContextChatReducer reducer,
        OperationalContextCollapseProjectionBuilder? projectionBuilder = null,
        OperationalContextCollapseSessionState? sessionState = null)
    {
        _reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
        _projectionBuilder = projectionBuilder ?? new OperationalContextCollapseProjectionBuilder();
        _sessionState = sessionState ?? new OperationalContextCollapseSessionState();
    }

    public IReadOnlyList<ChatMessage> PrepareMessages(
        IReadOnlyList<ChatMessage> requestMessages,
        AgentSession? session)
    {
        ArgumentNullException.ThrowIfNull(requestMessages);

        (IReadOnlyList<ChatMessage> messages, IReadOnlyList<string> projectedCollapseIds) = _projectionBuilder.BuildProjection(
            requestMessages,
            _sessionState.Get(session),
            _reducer.Options);
        _sessionState.RecordProjection(session, projectedCollapseIds);

        return messages;
    }

    public async ValueTask<IReadOnlyList<ChatMessage>> TryPrepareProactiveCollapseAsync(
        IReadOnlyList<ChatMessage> requestMessages,
        AgentSession? session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestMessages);

        IReadOnlyList<ChatMessage> committedProjectionMessages = PrepareMessages(requestMessages, session);
        int estimatedTokens = OperationalContextTokenEstimator.Estimate(committedProjectionMessages);
        bool shouldArm = estimatedTokens >= _reducer.Options.GetCollapseProactiveThreshold();
        _sessionState.RecordSpawnObservation(session, estimatedTokens, shouldArm);

        if (!shouldArm)
            return committedProjectionMessages;

        OperationalContextCompactionResult result =
            await _reducer.CompactReactiveAsync(committedProjectionMessages, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> pendingCollapseIds = StagePendingCollapse(
            committedProjectionMessages,
            session,
            result,
            OperationalContextCompactionReason.ContextCollapseProactive);

        if (estimatedTokens < _reducer.Options.GetCollapseBlockingThreshold() ||
            pendingCollapseIds.Count == 0)
            return committedProjectionMessages;

        CommitPendingCollapses(session, pendingCollapseIds);
        return PrepareMessages(requestMessages, session);
    }

    public async ValueTask<IReadOnlyList<ChatMessage>> PrepareReactiveRetryAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalMessages);

        IReadOnlyList<ChatMessage> committedProjectionMessages = PrepareMessages(originalMessages, session);
        OperationalContextCompactionResult result =
            await _reducer.CompactReactiveAsync(committedProjectionMessages, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> pendingCollapseIds = StagePendingCollapse(
            committedProjectionMessages,
            session,
            result,
            OperationalContextCompactionReason.Reactive);

        if (pendingCollapseIds.Count == 0)
            return committedProjectionMessages;

        CommitPendingCollapses(session, pendingCollapseIds);
        return PrepareMessages(originalMessages, session);
    }

    public HashSet<string> CaptureStagedCollapseIds(AgentSession? session) =>
        [.. _sessionState.Get(session).StagedSpans.Select(static span => span.CollapseId)];

    public HashSet<string> CaptureNewStagedCollapseIds(
        AgentSession? session,
        HashSet<string> initialStagedCollapseIds) =>
        [.. _sessionState
            .Get(session)
            .StagedSpans
            .Select(static span => span.CollapseId)
            .Where(collapseId => !initialStagedCollapseIds.Contains(collapseId))];

    public void CommitPendingCollapses(
        AgentSession? session,
        IEnumerable<string> collapseIds)
    {
        foreach (string collapseId in collapseIds)
            _sessionState.CommitStagedSpan(session, collapseId);
    }

    public void DiscardPendingCollapses(
        AgentSession? session,
        IEnumerable<string> collapseIds)
    {
        foreach (string collapseId in collapseIds)
            _sessionState.DiscardStagedSpan(session, collapseId);
    }

    private IReadOnlyList<string> StagePendingCollapse(
        IReadOnlyList<ChatMessage> baseMessages,
        AgentSession? session,
        OperationalContextCompactionResult result,
        OperationalContextCompactionReason reason)
    {
        if (!result.WasCompacted || result.ArchivedMessageReferences.Count == 0)
            return [];

        OperationalContextCollapseState state = _sessionState.StageCollapseSpan(
            session,
            result,
            reason);
        string? pendingCollapseId = state.Snapshot.LastStagedCollapseId;

        return string.IsNullOrWhiteSpace(pendingCollapseId) ? [] : [pendingCollapseId];
    }
}
