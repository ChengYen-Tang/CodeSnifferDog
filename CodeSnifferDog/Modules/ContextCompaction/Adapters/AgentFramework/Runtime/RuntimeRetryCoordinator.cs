using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

/// <summary>
/// Coordinates staged-projection retries and deep reactive retry preparation for runtime invocations.
/// </summary>
internal static class RuntimeRetryCoordinator
{
    /// <summary>
    /// Attempts a retry using newly committed collapse projections before falling back to deep reactive compaction.
    /// </summary>
    /// <typeparam name="T">Result type returned by the supplied retry delegate.</typeparam>
    /// <param name="originalMessages">Original request messages before any retry-specific projection changes.</param>
    /// <param name="collapseTracker">Tracker that exposes newly staged collapse identifiers for the current invocation.</param>
    /// <param name="options">Compaction options that define collapse mode and retry policy.</param>
    /// <param name="runAsync">Delegate that executes the retried invocation with replacement messages.</param>
    /// <returns>A success result when the staged retry succeeds; otherwise a not-run result.</returns>
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

    /// <summary>
    /// Prepares the deeply compacted retry transcript used after staged projection retry is unavailable or unsuccessful.
    /// </summary>
    /// <param name="originalMessages">Original request messages before retry preparation.</param>
    /// <param name="session">Agent session whose compaction state should be consulted.</param>
    /// <param name="options">Compaction options that define retry preparation behavior.</param>
    /// <param name="cancellationToken">Cancels retry preparation.</param>
    /// <returns>The deeply compacted retry messages.</returns>
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
