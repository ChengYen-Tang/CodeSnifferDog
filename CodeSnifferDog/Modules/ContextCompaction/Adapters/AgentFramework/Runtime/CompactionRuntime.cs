using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

internal static class CompactionRuntime
{
    public static async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        AgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        StagedCollapseTracker collapseTracker = new(session, options);

        try
        {
            AgentResponse response = await innerAgent.RunAsync(messages, session, runOptions, cancellationToken).ConfigureAwait(false);
            collapseTracker.CommitNew();
            return response;
        }
        catch (ModelInvocationException ex) when (ReactiveRetryService.ShouldRetry(options, ex))
        {
            IReadOnlyList<ChatMessage> originalMessages = [.. messages];
            StagedProjectionRetryResult<AgentResponse> stagedRetry = await RuntimeRetryCoordinator
                .TryRunStagedProjectionRetryAsync(
                    originalMessages,
                    collapseTracker,
                    options,
                    currentMessages => innerAgent.RunAsync(currentMessages, session, runOptions, cancellationToken))
                .ConfigureAwait(false);
            if (stagedRetry.Succeeded)
            {
                collapseTracker.CommitNew();
                return stagedRetry.Value!;
            }

            IReadOnlyList<ChatMessage> compactedMessages = await RuntimeRetryCoordinator.PrepareDeepReactiveRetryMessagesAsync(
                originalMessages,
                session,
                options,
                cancellationToken).ConfigureAwait(false);

            if (MessageEquivalenceComparer.AreEquivalent(originalMessages, compactedMessages))
                throw;

            try
            {
                AgentResponse response = await innerAgent.RunAsync(compactedMessages, session, runOptions, cancellationToken).ConfigureAwait(false);
                collapseTracker.CommitNew();
                return response;
            }
            catch
            {
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
            catch (ModelInvocationException ex) when (ReactiveRetryService.ShouldRetry(options, ex))
            {
                try
                {
                    IReadOnlyList<ChatMessage> originalMessages = [.. messages];
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
                        collapseTracker.CommitNew();
                        return;
                    }

                    IReadOnlyList<ChatMessage> compactedMessages = await RuntimeRetryCoordinator.PrepareDeepReactiveRetryMessagesAsync(
                        originalMessages,
                        session,
                        options,
                        cancellationToken).ConfigureAwait(false);

                    if (MessageEquivalenceComparer.AreEquivalent(originalMessages, compactedMessages))
                        throw;

                    try
                    {
                        await pump.PumpAsync(compactedMessages).ConfigureAwait(false);
                        collapseTracker.CommitNew();
                    }
                    catch
                    {
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
}
