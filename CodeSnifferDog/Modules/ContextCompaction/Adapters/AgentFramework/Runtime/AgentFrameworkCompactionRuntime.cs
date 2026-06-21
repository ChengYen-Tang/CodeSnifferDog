using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Threading.Channels;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

internal static class AgentFrameworkCompactionRuntime
{
    public static async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        OperationalContextAgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        HashSet<string> initialStagedCollapseIds = CaptureStagedCollapseIds(session, options);

        try
        {
            AgentResponse response = await innerAgent.RunAsync(messages, session, runOptions, cancellationToken).ConfigureAwait(false);
            CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
            return response;
        }
        catch (OperationalContextModelInvocationException ex) when (ReactiveRetryService.ShouldRetry(options, ex))
        {
            HashSet<string> stagedCollapseIdsAfterFailure = CaptureNewStagedCollapseIds(session, options, initialStagedCollapseIds);
            if (stagedCollapseIdsAfterFailure.Count > 0 &&
                options.Reducer.Options.Mode == OperationalContextCompactionMode.ContextCollapse)
            {
                IReadOnlyList<ChatMessage> committedProjectionMessages = CommitStagedCollapsesAndPrepareRetryMessages(
                    [.. messages],
                    session,
                    options,
                    stagedCollapseIdsAfterFailure);

                if (!MessageEquivalenceComparer.AreEquivalent([.. messages], committedProjectionMessages))
                {
                    try
                    {
                        AgentResponse response = await innerAgent.RunAsync(committedProjectionMessages, session, runOptions, cancellationToken).ConfigureAwait(false);
                        CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
                        return response;
                    }
                    catch (OperationalContextModelInvocationException retryEx) when (ReactiveRetryService.ShouldRetry(options, retryEx))
                    {
                        // Fall through to a deeper collapse-owned reactive retry.
                    }
                }
            }

            IReadOnlyList<ChatMessage> originalMessages = [.. messages];
            ReactiveRetryPreparation retryPreparation = await ReactiveRetryService.PrepareAsync(
                originalMessages,
                session,
                options,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ChatMessage> compactedMessages = retryPreparation.Messages;

            if (MessageEquivalenceComparer.AreEquivalent(originalMessages, compactedMessages))
                throw;

            try
            {
                AgentResponse response = await innerAgent.RunAsync(compactedMessages, session, runOptions, cancellationToken).ConfigureAwait(false);
                CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
                return response;
            }
            catch
            {
                DiscardNewStagedCollapses(session, options, initialStagedCollapseIds);
                throw;
            }
        }
        catch
        {
            DiscardNewStagedCollapses(session, options, initialStagedCollapseIds);
            throw;
        }
    }

    public static IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        OperationalContextAgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        Channel<AgentResponseUpdate> updates = Channel.CreateBounded<AgentResponseUpdate>(1);
        HashSet<string> initialStagedCollapseIds = CaptureStagedCollapseIds(session, options);

        _ = ProcessAsync();
        return updates.Reader.ReadAllAsync(cancellationToken);

        async Task ProcessAsync()
        {
            Exception? error = null;

            try
            {
                await PumpAsync(messages).ConfigureAwait(false);
                CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
            }
            catch (OperationalContextModelInvocationException ex) when (ReactiveRetryService.ShouldRetry(options, ex))
            {
                try
                {
                    HashSet<string> stagedCollapseIdsAfterFailure = CaptureNewStagedCollapseIds(session, options, initialStagedCollapseIds);
                    if (stagedCollapseIdsAfterFailure.Count > 0 &&
                        options.Reducer.Options.Mode == OperationalContextCompactionMode.ContextCollapse)
                    {
                        IReadOnlyList<ChatMessage> committedProjectionMessages = CommitStagedCollapsesAndPrepareRetryMessages(
                            [.. messages],
                            session,
                            options,
                            stagedCollapseIdsAfterFailure);

                        if (!MessageEquivalenceComparer.AreEquivalent([.. messages], committedProjectionMessages))
                        {
                            try
                            {
                                await PumpAsync(committedProjectionMessages).ConfigureAwait(false);
                                CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
                                return;
                            }
                            catch (OperationalContextModelInvocationException retryEx) when (ReactiveRetryService.ShouldRetry(options, retryEx))
                            {
                                // Fall through to a deeper collapse-owned reactive retry.
                            }
                        }
                    }

                    IReadOnlyList<ChatMessage> originalMessages = [.. messages];
                    ReactiveRetryPreparation retryPreparation = await ReactiveRetryService.PrepareAsync(
                        originalMessages,
                        session,
                        options,
                        cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<ChatMessage> compactedMessages = retryPreparation.Messages;

                    if (MessageEquivalenceComparer.AreEquivalent(originalMessages, compactedMessages))
                        throw;

                    try
                    {
                        await PumpAsync(compactedMessages).ConfigureAwait(false);
                        CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
                    }
                    catch
                    {
                        DiscardNewStagedCollapses(session, options, initialStagedCollapseIds);
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
                DiscardNewStagedCollapses(session, options, initialStagedCollapseIds);
                error = ex;
            }
            finally
            {
                updates.Writer.TryComplete(error);
            }
        }

        async Task PumpAsync(IEnumerable<ChatMessage> currentMessages)
        {
            await foreach (AgentResponseUpdate update in innerAgent.RunStreamingAsync(currentMessages, session, runOptions, cancellationToken).ConfigureAwait(false))
                await updates.Writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<ChatMessage> CommitStagedCollapsesAndPrepareRetryMessages(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        IEnumerable<string> collapseIds)
    {
        CommitStagedCollapses(session, options, collapseIds);
        return options.CollapseController?.PrepareMessages(originalMessages, session) ?? originalMessages;
    }

    private static HashSet<string> CaptureStagedCollapseIds(
        AgentSession? session,
        OperationalContextAgentCompactionOptions options) =>
        options.CollapseController is null
            ? []
            : options.CollapseController.CaptureStagedCollapseIds(session);

    private static HashSet<string> CaptureNewStagedCollapseIds(
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        HashSet<string> initialStagedCollapseIds)
    {
        if (options.CollapseController is null)
            return [];

        return options.CollapseController.CaptureNewStagedCollapseIds(session, initialStagedCollapseIds);
    }

    private static void CommitNewStagedCollapses(
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        HashSet<string> initialStagedCollapseIds)
    {
        if (options.CollapseController is null)
            return;

        CommitStagedCollapses(session, options, CaptureNewStagedCollapseIds(session, options, initialStagedCollapseIds));
    }

    private static void DiscardNewStagedCollapses(
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        HashSet<string> initialStagedCollapseIds)
    {
        if (options.CollapseController is null)
            return;

        options.CollapseController.DiscardPendingCollapses(
            session,
            CaptureNewStagedCollapseIds(session, options, initialStagedCollapseIds));
    }

    private static void CommitStagedCollapses(
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        IEnumerable<string> collapseIds)
        => options.CollapseController?.CommitPendingCollapses(session, collapseIds);
}
