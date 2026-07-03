using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

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
