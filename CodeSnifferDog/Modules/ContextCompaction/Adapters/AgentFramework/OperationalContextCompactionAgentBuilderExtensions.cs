using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading.Channels;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public static class OperationalContextCompactionAgentBuilderExtensions
{
    public static AIAgentBuilder UseOperationalContextCompaction(
        this AIAgentBuilder builder,
        OperationalContextAgentCompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Reducer);
        ArgumentNullException.ThrowIfNull(options.ReactiveExceptionDecider);

        if (options.Reducer.Options.Mode == OperationalContextCompactionMode.ContextCollapse &&
            options.CollapseController is null)
            throw new InvalidOperationException("ContextCollapse mode requires an OperationalContextCollapseController.");

        builder.UseAIContextProviders(new OperationalContextCompactionMessageContextProvider(options));

        return builder.Use(
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                RunAndTrackAsync(messages, session, runOptions, innerAgent, options, cancellationToken),
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                RunStreamingAndTrackAsync(messages, session, runOptions, innerAgent, options, cancellationToken));
    }

    public static async Task InvokeWithReactiveCompactionRetryAsync(
        IReadOnlyList<ChatMessage> messages,
        OperationalContextAgentCompactionOptions options,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await next(messages, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationalContextModelInvocationException ex) when (
            options.EnableReactiveCompactionRetry &&
            options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(ex))
        {
            ReactiveRetryPreparation retryPreparation = await PrepareReactiveRetryAsync(
                messages,
                null,
                options,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ChatMessage> compactedMessages = retryPreparation.Messages;

            if (MessagesAreEquivalentForRetry(messages, compactedMessages))
                throw;

            await next(compactedMessages, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static bool MessagesAreEquivalentForRetry(
        IReadOnlyList<ChatMessage> left,
        IReadOnlyList<ChatMessage> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
            if (left[index].Role != right[index].Role ||
                !string.Equals(left[index].Text, right[index].Text, StringComparison.Ordinal) ||
                !ContentsAreEquivalent(left[index].Contents, right[index].Contents) ||
                !AdditionalPropertiesAreEquivalent(left[index].AdditionalProperties, right[index].AdditionalProperties))
                return false;

        return true;
    }

    private static bool ContentsAreEquivalent(IList<AIContent> left, IList<AIContent> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
            if (!string.Equals(CanonicalizeContent(left[index]), CanonicalizeContent(right[index]), StringComparison.Ordinal))
                return false;

        return true;
    }

    private static bool AdditionalPropertiesAreEquivalent(
        AdditionalPropertiesDictionary? left,
        AdditionalPropertiesDictionary? right) =>
        CanonicalizeAdditionalProperties(left) == CanonicalizeAdditionalProperties(right);

    private static string CanonicalizeContent(AIContent content)
    {
        IEnumerable<string> entries = content.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={CanonicalizeValue(property.GetValue(content))}");

        return $"{content.GetType().FullName}|{string.Join("|", entries)}";
    }

    private static string CanonicalizeAdditionalProperties(AdditionalPropertiesDictionary? properties)
    {
        if (properties is null || properties.Count == 0)
            return string.Empty;

        IEnumerable<string> entries = properties
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key}={CanonicalizeValue(pair.Value)}");

        return string.Join("|", entries);
    }

    private static string CanonicalizeValue(object? value)
    {
        if (value is null)
            return "<null>";

        if (value is string text)
            return text;

        if (value is bool boolean)
            return boolean ? "true" : "false";

        if (value is Enum enumValue)
            return enumValue.ToString();

        if (value is IDictionary dictionary)
        {
            List<string> entries = [];

            foreach (DictionaryEntry entry in dictionary)
                entries.Add($"{entry.Key}={CanonicalizeValue(entry.Value)}");

            return string.Join(",", entries.OrderBy(static item => item, StringComparer.Ordinal));
        }

        if (value is IEnumerable enumerable)
        {
            List<string> items = [];

            foreach (object? item in enumerable)
                items.Add(CanonicalizeValue(item));

            return $"[{string.Join(",", items)}]";
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    private static async Task<AgentResponse> RunAndTrackAsync(
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
        catch (OperationalContextModelInvocationException ex) when (
            options.EnableReactiveCompactionRetry &&
            options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(ex))
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

                if (!MessagesAreEquivalentForRetry([.. messages], committedProjectionMessages))
                {
                    try
                    {
                        AgentResponse response = await innerAgent.RunAsync(committedProjectionMessages, session, runOptions, cancellationToken).ConfigureAwait(false);
                        CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
                        return response;
                    }
                    catch (OperationalContextModelInvocationException retryEx) when (
                        options.EnableReactiveCompactionRetry &&
                        options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(retryEx))
                    {
                        // Fall through to a deeper collapse-owned reactive retry.
                    }
                }
            }

            IReadOnlyList<ChatMessage> originalMessages = [.. messages];
            ReactiveRetryPreparation retryPreparation = await PrepareReactiveRetryAsync(
                originalMessages,
                session,
                options,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ChatMessage> compactedMessages = retryPreparation.Messages;

            if (MessagesAreEquivalentForRetry(originalMessages, compactedMessages))
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

    private static IAsyncEnumerable<AgentResponseUpdate> RunStreamingAndTrackAsync(
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
            catch (OperationalContextModelInvocationException ex) when (
                options.EnableReactiveCompactionRetry &&
                options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(ex))
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

                        if (!MessagesAreEquivalentForRetry([.. messages], committedProjectionMessages))
                        {
                            try
                            {
                                await PumpAsync(committedProjectionMessages).ConfigureAwait(false);
                                CommitNewStagedCollapses(session, options, initialStagedCollapseIds);
                                return;
                            }
                            catch (OperationalContextModelInvocationException retryEx) when (
                                options.EnableReactiveCompactionRetry &&
                                options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(retryEx))
                            {
                                // Fall through to a deeper collapse-owned reactive retry.
                            }
                        }
                    }

                    IReadOnlyList<ChatMessage> originalMessages = [.. messages];
                    ReactiveRetryPreparation retryPreparation = await PrepareReactiveRetryAsync(
                        originalMessages,
                        session,
                        options,
                        cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<ChatMessage> compactedMessages = retryPreparation.Messages;

                    if (MessagesAreEquivalentForRetry(originalMessages, compactedMessages))
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
            {
                await updates.Writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
            }
        }
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

    private static IReadOnlyList<ChatMessage> CommitStagedCollapsesAndPrepareRetryMessages(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        IEnumerable<string> collapseIds)
    {
        CommitStagedCollapses(session, options, collapseIds);
        return options.CollapseController?.PrepareMessages(originalMessages, session) ?? originalMessages;
    }

    private static async Task<ReactiveRetryPreparation> PrepareReactiveRetryAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        OperationalContextAgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Reducer.Options.Mode == OperationalContextCompactionMode.ContextCollapse)
            return new ReactiveRetryPreparation
            {
                Messages = await options.CollapseController!
                    .PrepareReactiveRetryAsync(originalMessages, session, cancellationToken)
                    .ConfigureAwait(false),
            };

        IReadOnlyList<ChatMessage> retryMessages = OperationalContextMessageShrinker.ApplySnip(originalMessages, options.Reducer.Options).Messages;
        OperationalContextCompactionResult result =
            await options.Reducer.CompactReactiveAsync(retryMessages, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChatMessage> compactedMessages = OperationalContextChatReducer.BuildMessages(result);

        return new ReactiveRetryPreparation
        {
            Messages = compactedMessages,
        };
    }

    private sealed class ReactiveRetryPreparation
    {
        public required IReadOnlyList<ChatMessage> Messages { get; init; }
    }
}
