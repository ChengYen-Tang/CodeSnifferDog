using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

[TestClass]
public sealed class StreamingCompactionRuntimeTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunStreamingAsync_CommitsNewStagedCollapse_OnSuccess()
    {
        (AgentCompactionOptions options, CollapseSessionState sessionState) = CreateContextCollapseOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("success");
        ScriptedStreamingAgent agent = new((_, currentSession, cancellationToken) =>
            StageAndYieldAsync(options, sessionState, currentSession!, messages, "ok", cancellationToken));

        AgentResponseUpdate[] updates =
        [
            .. await CompactionRuntime
                .RunStreamingAsync(messages, session, null, agent, options, TestContext.CancellationToken)
                .ToArrayAsync(TestContext.CancellationToken),
        ];

        Assert.HasCount(1, updates);
        Assert.AreEqual("ok", updates[0].Text);
        CollapseState state = sessionState.Get(session);
        Assert.HasCount(1, state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    [TestMethod]
    public async Task RunStreamingAsync_CompletesReaderWithOriginalError_AndDiscardsStagedCollapse()
    {
        (AgentCompactionOptions options, CollapseSessionState sessionState) = CreateContextCollapseOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("failure");
        ScriptedStreamingAgent agent = new((_, currentSession, cancellationToken) =>
            StageAndThrowAsync(options, sessionState, currentSession!, messages, cancellationToken));

        ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
            async () =>
            {
                await foreach (AgentResponseUpdate _ in CompactionRuntime
                    .RunStreamingAsync(messages, session, null, agent, options, TestContext.CancellationToken)
                    .ConfigureAwait(false))
                {
                }
            });

        Assert.AreEqual("stream failed", exception.Message);
        CollapseState state = sessionState.Get(session);
        Assert.IsEmpty(state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    [TestMethod]
    public async Task RunStreamingAsync_EmitsRetryUpdates_WhenReactiveRetrySucceeds()
    {
        AgentCompactionOptions options = CreateStandardOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("retry");
        Queue<Func<IReadOnlyList<ChatMessage>, AgentSession?, CancellationToken, IAsyncEnumerable<AgentResponseUpdate>>> behaviors = new(
        [
            (_, _, _) => ThrowCompactableAsync(),
            (_, _, _) => YieldAsync("retry-ok"),
        ]);
        ScriptedStreamingAgent agent = new((currentMessages, currentSession, cancellationToken) =>
            behaviors.Dequeue()(currentMessages, currentSession, cancellationToken));

        AgentResponseUpdate[] updates =
        [
            .. await CompactionRuntime
                .RunStreamingAsync(messages, session, null, agent, options, TestContext.CancellationToken)
                .ToArrayAsync(TestContext.CancellationToken),
        ];

        Assert.HasCount(1, updates);
        Assert.AreEqual("retry-ok", updates[0].Text);
    }

    [TestMethod]
    public async Task RunStreamingAsync_PropagatesCancellation()
    {
        AgentCompactionOptions options = CreateStandardOptions();
        using CancellationTokenSource cancellation = new();
        ScriptedStreamingAgent agent = new((_, _, cancellationToken) => WaitUntilCanceledAsync(cancellationToken));
        IAsyncEnumerable<AgentResponseUpdate> updates = CompactionRuntime.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "cancel")],
            new TestSession(),
            null,
            agent,
            options,
            cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await foreach (AgentResponseUpdate _ in updates.WithCancellation(cancellation.Token).ConfigureAwait(false))
            {
            }
        });
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> StageAndYieldAsync(
        AgentCompactionOptions options,
        CollapseSessionState sessionState,
        AgentSession session,
        IReadOnlyList<ChatMessage> messages,
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await StageCollapseAsync(options, sessionState, session, messages, cancellationToken);
        yield return new AgentResponseUpdate(ChatRole.Assistant, text);
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> StageAndThrowAsync(
        AgentCompactionOptions options,
        CollapseSessionState sessionState,
        AgentSession session,
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await StageCollapseAsync(options, sessionState, session, messages, cancellationToken);
        throw new ModelInvocationException(
            ModelInvocationFailureKind.Unknown,
            "stream failed");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> ThrowCompactableAsync()
    {
        await Task.Yield();
        throw new ModelInvocationException(
            ModelInvocationFailureKind.ContextWindowExceeded,
            "context too large");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> YieldAsync(string text)
    {
        await Task.Yield();
        yield return new AgentResponseUpdate(ChatRole.Assistant, text);
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> WaitUntilCanceledAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield break;
    }

    private static async Task StageCollapseAsync(
        AgentCompactionOptions options,
        CollapseSessionState sessionState,
        AgentSession session,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        CompactionResult result = await options.Reducer
            .CompactReactiveAsync(messages, cancellationToken)
            .ConfigureAwait(false);
        sessionState.StageCollapseSpan(session, result, CompactionReason.ContextCollapseProactive);
    }

    private static ChatMessage[] CreateMessages(string suffix) =>
    [
        new(ChatRole.User, $"user-{suffix}"),
        new(ChatRole.Assistant, $"assistant-{suffix}"),
        new(ChatRole.User, new string('x', 1_000)),
    ];

    private static (AgentCompactionOptions Options, CollapseSessionState SessionState) CreateContextCollapseOptions()
    {
        CollapseSessionState sessionState = new();
        ChatReducer reducer = CreateReducer(CompactionMode.ContextCollapse);

        return (new AgentCompactionOptions
        {
            Reducer = reducer,
            CollapseController = new CollapseController(reducer, sessionState: sessionState),
        }, sessionState);
    }

    private static AgentCompactionOptions CreateStandardOptions()
    {
        ChatReducer reducer = CreateReducer(CompactionMode.Standard);

        return new AgentCompactionOptions
        {
            Reducer = reducer,
        };
    }

    private static ChatReducer CreateReducer(CompactionMode mode) => new(
        new CompactionOptions
        {
            ModelContextWindowTokens = 100_000,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 1,
            Mode = mode,
        },
        new StaticSummaryPromptProvider("summarize"),
        new RecordingSummarizer());

    private sealed class RecordingSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult("<summary>Current objective\nCompleted work\nNext steps</summary>");
    }

    private sealed class ScriptedStreamingAgent(
        Func<IReadOnlyList<ChatMessage>, AgentSession?, CancellationToken, IAsyncEnumerable<AgentResponseUpdate>> streamAsync) : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AgentSession>(new TestSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await foreach (AgentResponseUpdate update in streamAsync([.. messages], session, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
                yield return update;
        }
    }

    private sealed class TestSession : AgentSession;
}
