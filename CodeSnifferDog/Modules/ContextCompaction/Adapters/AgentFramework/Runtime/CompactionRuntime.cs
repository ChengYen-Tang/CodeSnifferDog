using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

/// <summary>
/// Wraps agent execution with staged-collapse bookkeeping and reactive compaction retry behavior.
/// </summary>
internal static class CompactionRuntime
{
    /// <summary>
    /// Runs a non-streaming agent invocation with staged-collapse commit/rollback and reactive retry handling.
    /// </summary>
    /// <param name="messages">Messages to send to the inner agent.</param>
    /// <param name="session">Agent session associated with the invocation.</param>
    /// <param name="runOptions">Optional run options passed to the inner agent.</param>
    /// <param name="innerAgent">Inner agent that performs the actual invocation.</param>
    /// <param name="options">Compaction options that control collapse and retry behavior.</param>
    /// <param name="cancellationToken">Cancels the invocation.</param>
    /// <returns>The successful agent response.</returns>
    public static async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        AgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        StagedCollapseTracker collapseTracker = new(session, options);
        ILogger logger = CreateLogger(options);

        try
        {
            AgentResponse response = await innerAgent.RunAsync(messages, session, runOptions, cancellationToken).ConfigureAwait(false);
            collapseTracker.CommitNew();
            return response;
        }
        catch (Exception ex) when (ReactiveRetryService.ShouldRetry(options, ex))
        {
            IReadOnlyList<ChatMessage> originalMessages = [.. messages];
            logger.LogDebug(
                ex,
                "Reactive compaction retry triggered. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                originalMessages.Count,
                TokenEstimator.Estimate(originalMessages));

            StagedProjectionRetryResult<AgentResponse> stagedRetry = await RuntimeRetryCoordinator
                .TryRunStagedProjectionRetryAsync(
                    originalMessages,
                    collapseTracker,
                    options,
                    currentMessages => innerAgent.RunAsync(currentMessages, session, runOptions, cancellationToken))
                .ConfigureAwait(false);
            if (stagedRetry.Succeeded)
            {
                logger.LogDebug("Reactive staged projection retry succeeded.");
                collapseTracker.CommitNew();
                return stagedRetry.Value!;
            }

            IReadOnlyList<ChatMessage> compactedMessages = await RuntimeRetryCoordinator.PrepareDeepReactiveRetryMessagesAsync(
                originalMessages,
                session,
                options,
                cancellationToken).ConfigureAwait(false);

            if (MessageEquivalenceComparer.AreEquivalent(originalMessages, compactedMessages))
            {
                logger.LogWarning(
                    "Reactive deep compaction produced equivalent messages. Re-throwing original model invocation exception. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                    originalMessages.Count,
                    TokenEstimator.Estimate(originalMessages));

                throw;
            }

            logger.LogDebug(
                "Reactive deep compaction prepared retry messages. OriginalMessageCount: {OriginalMessageCount}; MessageCount: {MessageCount}; EstimatedTokensBefore: {EstimatedTokensBefore}; EstimatedTokensAfter: {EstimatedTokensAfter}.",
                originalMessages.Count,
                compactedMessages.Count,
                TokenEstimator.Estimate(originalMessages),
                TokenEstimator.Estimate(compactedMessages));

            try
            {
                AgentResponse response = await AgentRunAttemptContext
                    .RunWithPreCompactedContextAsync(
                        () => innerAgent.RunAsync(compactedMessages, session, runOptions, cancellationToken))
                    .ConfigureAwait(false);
                logger.LogDebug("Reactive deep compaction retry succeeded.");
                collapseTracker.CommitNew();
                return response;
            }
            catch
            {
                logger.LogWarning("Reactive deep compaction retry failed. Discarding staged collapse state.");
                collapseTracker.DiscardNew();
                throw;
            }
        }
        catch
        {
            collapseTracker.DiscardNew();
            throw;
        }
    }

    /// <summary>
    /// Runs a streaming agent invocation with staged-collapse commit/rollback and reactive retry handling.
    /// </summary>
    /// <param name="messages">Messages to send to the inner agent.</param>
    /// <param name="session">Agent session associated with the invocation.</param>
    /// <param name="runOptions">Optional run options passed to the inner agent.</param>
    /// <param name="innerAgent">Inner agent that performs the actual invocation.</param>
    /// <param name="options">Compaction options that control collapse and retry behavior.</param>
    /// <param name="cancellationToken">Cancels the invocation.</param>
    /// <returns>An async stream of response updates from the successful run or retry.</returns>
    public static IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        AgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        StreamingUpdatePump pump = new(innerAgent, session, runOptions, cancellationToken);
        StagedCollapseTracker collapseTracker = new(session, options);
        ILogger logger = CreateLogger(options);

        _ = ProcessAsync();
        return pump.ReadAllAsync();

        async Task ProcessAsync()
        {
            Exception? error = null;

            try
            {
                await pump.PumpAsync(messages).ConfigureAwait(false);
                collapseTracker.CommitNew();
            }
            catch (Exception ex) when (ReactiveRetryService.ShouldRetry(options, ex))
            {
                try
                {
                    IReadOnlyList<ChatMessage> originalMessages = [.. messages];
                    logger.LogDebug(
                        ex,
                        "Reactive streaming compaction retry triggered. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                        originalMessages.Count,
                        TokenEstimator.Estimate(originalMessages));

                    StagedProjectionRetryResult<bool> stagedRetry = await RuntimeRetryCoordinator
                        .TryRunStagedProjectionRetryAsync(
                            originalMessages,
                            collapseTracker,
                            options,
                            async currentMessages =>
                            {
                                await pump.PumpAsync(currentMessages).ConfigureAwait(false);
                                return true;
                            })
                        .ConfigureAwait(false);
                    if (stagedRetry.Succeeded)
                    {
                        logger.LogDebug("Reactive streaming staged projection retry succeeded.");
                        collapseTracker.CommitNew();
                        return;
                    }

                    IReadOnlyList<ChatMessage> compactedMessages = await RuntimeRetryCoordinator.PrepareDeepReactiveRetryMessagesAsync(
                        originalMessages,
                        session,
                        options,
                        cancellationToken).ConfigureAwait(false);

                    if (MessageEquivalenceComparer.AreEquivalent(originalMessages, compactedMessages))
                    {
                        logger.LogWarning(
                            "Reactive streaming deep compaction produced equivalent messages. Re-throwing original model invocation exception. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                            originalMessages.Count,
                            TokenEstimator.Estimate(originalMessages));

                        throw;
                    }

                    logger.LogDebug(
                        "Reactive streaming deep compaction prepared retry messages. OriginalMessageCount: {OriginalMessageCount}; MessageCount: {MessageCount}; EstimatedTokensBefore: {EstimatedTokensBefore}; EstimatedTokensAfter: {EstimatedTokensAfter}.",
                        originalMessages.Count,
                        compactedMessages.Count,
                        TokenEstimator.Estimate(originalMessages),
                        TokenEstimator.Estimate(compactedMessages));

                    try
                    {
                        await AgentRunAttemptContext
                            .RunWithPreCompactedContextAsync(() => pump.PumpAsync(compactedMessages))
                            .ConfigureAwait(false);
                        logger.LogDebug("Reactive streaming deep compaction retry succeeded.");
                        collapseTracker.CommitNew();
                    }
                    catch
                    {
                        logger.LogWarning("Reactive streaming deep compaction retry failed. Discarding staged collapse state.");
                        collapseTracker.DiscardNew();
                        throw;
                    }
                }
                catch (Exception retryEx)
                {
                    error = retryEx;
                }
            }
            catch (Exception ex)
            {
                collapseTracker.DiscardNew();
                error = ex;
            }
            finally
            {
                pump.Complete(error);
            }
        }
    }

    private static ILogger CreateLogger(AgentCompactionOptions options) =>
        options.LoggerFactory?.CreateLogger(typeof(CompactionRuntime).FullName!) ?? NullLogger.Instance;
}
