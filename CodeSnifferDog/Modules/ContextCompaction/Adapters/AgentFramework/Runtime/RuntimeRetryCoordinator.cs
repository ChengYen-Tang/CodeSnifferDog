using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

internal static class RuntimeRetryCoordinator
{
    public static async Task<StagedProjectionRetryResult<T>> TryRunStagedProjectionRetryAsync<T>(
        IReadOnlyList<ChatMessage> originalMessages,
        StagedCollapseTracker collapseTracker,
        AgentCompactionOptions options,
        Func<IReadOnlyList<ChatMessage>, Task<T>> runAsync)
    {
        HashSet<string> stagedCollapseIdsAfterFailure = collapseTracker.CaptureNewIds();
        if (stagedCollapseIdsAfterFailure.Count == 0 ||
            options.Reducer.Options.Mode != CompactionMode.ContextCollapse)
            return StagedProjectionRetryResult<T>.NotRun();

        IReadOnlyList<ChatMessage> committedProjectionMessages = collapseTracker.CommitAndPrepareRetryMessages(
            originalMessages,
            stagedCollapseIdsAfterFailure);

        if (MessageEquivalenceComparer.AreEquivalent(originalMessages, committedProjectionMessages))
            return StagedProjectionRetryResult<T>.NotRun();

        try
        {
            return StagedProjectionRetryResult<T>.Success(await runAsync(committedProjectionMessages).ConfigureAwait(false));
        }
        catch (ModelInvocationException retryEx) when (ReactiveRetryService.ShouldRetry(options, retryEx))
        {
            return StagedProjectionRetryResult<T>.NotRun();
        }
    }

    public static async Task<IReadOnlyList<ChatMessage>> PrepareDeepReactiveRetryMessagesAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        AgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        ReactiveRetryPreparation retryPreparation = await ReactiveRetryService.PrepareAsync(
            originalMessages,
            session,
            options,
            cancellationToken).ConfigureAwait(false);

        return retryPreparation.Messages;
    }
}
