using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

internal sealed class StagedCollapseTracker(
    AgentSession? session,
    OperationalContextAgentCompactionOptions options)
{
    private readonly HashSet<string> _initialStagedCollapseIds = CaptureStagedCollapseIds(session, options);

    public HashSet<string> CaptureNewIds()
    {
        if (options.CollapseController is null)
            return [];

        return options.CollapseController.CaptureNewStagedCollapseIds(session, _initialStagedCollapseIds);
    }

    public void CommitNew()
    {
        if (options.CollapseController is null)
            return;

        Commit(CaptureNewIds());
    }

    public void DiscardNew()
    {
        if (options.CollapseController is null)
            return;

        options.CollapseController.DiscardPendingCollapses(session, CaptureNewIds());
    }

    public IReadOnlyList<ChatMessage> CommitAndPrepareRetryMessages(
        IReadOnlyList<ChatMessage> originalMessages,
        IEnumerable<string> collapseIds)
    {
        Commit(collapseIds);
        return options.CollapseController?.PrepareMessages(originalMessages, session) ?? originalMessages;
    }

    private void Commit(IEnumerable<string> collapseIds) =>
        options.CollapseController?.CommitPendingCollapses(session, collapseIds);

    private static HashSet<string> CaptureStagedCollapseIds(
        AgentSession? session,
        OperationalContextAgentCompactionOptions options) =>
        options.CollapseController is null
            ? []
            : options.CollapseController.CaptureStagedCollapseIds(session);
}
