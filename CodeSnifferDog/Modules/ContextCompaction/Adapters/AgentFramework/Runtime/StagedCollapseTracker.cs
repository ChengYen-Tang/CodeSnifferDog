using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

/// <summary>
/// Tracks the staged collapse identifiers created during one runtime invocation so they can be committed or discarded atomically.
/// </summary>
/// <param name="session">Session whose collapse state is being observed.</param>
/// <param name="options">Compaction options that expose the optional collapse controller.</param>
internal sealed class StagedCollapseTracker(
    AgentSession? session,
    AgentCompactionOptions options)
{
    /// <summary>
    /// Snapshot of staged collapse identifiers that existed before the current invocation started.
    /// </summary>
    private readonly HashSet<string> _initialStagedCollapseIds = CaptureStagedCollapseIds(session, options);

    /// <summary>
    /// Captures the staged collapse identifiers that were added after this tracker was created.
    /// </summary>
    /// <returns>The newly staged collapse identifiers.</returns>
    public HashSet<string> CaptureNewIds()
    {
        if (options.CollapseController is null)
            return [];

        return options.CollapseController.CaptureNewStagedCollapseIds(session, _initialStagedCollapseIds);
    }

    /// <summary>
    /// Commits every newly staged collapse span observed by this tracker.
    /// </summary>
    public void CommitNew()
    {
        if (options.CollapseController is null)
            return;

        Commit(CaptureNewIds());
    }

    /// <summary>
    /// Discards every newly staged collapse span observed by this tracker.
    /// </summary>
    public void DiscardNew()
    {
        if (options.CollapseController is null)
            return;

        options.CollapseController.DiscardPendingCollapses(session, CaptureNewIds());
    }

    /// <summary>
    /// Commits the supplied collapse identifiers and rebuilds the retry transcript from the committed projection.
    /// </summary>
    /// <param name="originalMessages">Original request messages before projection.</param>
    /// <param name="collapseIds">Staged collapse identifiers to commit.</param>
    /// <returns>The retry transcript after the committed projection has been applied.</returns>
    public IReadOnlyList<ChatMessage> CommitAndPrepareRetryMessages(
        IReadOnlyList<ChatMessage> originalMessages,
        IEnumerable<string> collapseIds)
    {
        Commit(collapseIds);
        return options.CollapseController?.PrepareMessages(originalMessages, session) ?? originalMessages;
    }

    /// <summary>
    /// Commits the supplied collapse identifiers when a collapse controller is configured.
    /// </summary>
    /// <param name="collapseIds">Collapse identifiers to commit.</param>
    private void Commit(IEnumerable<string> collapseIds) =>
        options.CollapseController?.CommitPendingCollapses(session, collapseIds);

    /// <summary>
    /// Captures the currently staged collapse identifiers for the supplied session.
    /// </summary>
    /// <param name="session">Session whose staged collapse identifiers should be captured.</param>
    /// <param name="options">Compaction options that expose the optional collapse controller.</param>
    /// <returns>The currently staged collapse identifiers, or an empty set when collapse projection is disabled.</returns>
    private static HashSet<string> CaptureStagedCollapseIds(
        AgentSession? session,
        AgentCompactionOptions options) =>
        options.CollapseController is null
            ? []
            : options.CollapseController.CaptureStagedCollapseIds(session);
}
