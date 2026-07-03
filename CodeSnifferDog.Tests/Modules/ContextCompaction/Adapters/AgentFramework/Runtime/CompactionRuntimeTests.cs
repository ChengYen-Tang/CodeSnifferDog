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
public sealed class CompactionRuntimeTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_CommitsNewStagedCollapse_OnSuccess()
    {
        (AgentCompactionOptions options, CollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("success");
        ScriptedAgent agent = new(async (_, currentSession, cancellationToken) =>
        {
            await StageCollapseAsync(options, sessionState, currentSession!, messages, cancellationToken);
            return new AgentResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        });

        _ = await CompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken);

        CollapseState state = sessionState.Get(session);
        Assert.HasCount(1, state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    [TestMethod]
    public async Task RunAsync_DiscardsNewStagedCollapse_WhenRunFailsWithoutRetry()
    {
        (AgentCompactionOptions options, CollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("failure");
        ScriptedAgent agent = new(async (_, currentSession, cancellationToken) =>
        {
            await StageCollapseAsync(options, sessionState, currentSession!, messages, cancellationToken);
            throw new ModelInvocationException(
                ModelInvocationFailureKind.Unknown,
                "boom");
        });

        await Assert.ThrowsExactlyAsync<ModelInvocationException>(
            () => CompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken));

        CollapseState state = sessionState.Get(session);
        Assert.IsEmpty(state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    [TestMethod]
    public async Task RunAsync_PreservesOriginalException_WhenReactiveRetryMessagesAreEquivalent()
    {
        (AgentCompactionOptions options, _) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = [new(ChatRole.User, "single-message")];
        ScriptedAgent agent = new((_, _, _) => throw new ModelInvocationException(
            ModelInvocationFailureKind.ContextWindowExceeded,
            "original"));

        ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
            () => CompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken));

        Assert.AreEqual("original", exception.Message);
    }

    private static ChatMessage[] CreateMessages(string suffix) =>
    [
        new(ChatRole.User, $"user-{suffix}"),
        new(ChatRole.Assistant, $"assistant-{suffix}"),
        new(ChatRole.User, new string('x', 1_000)),
    ];

    private static (AgentCompactionOptions Options, CollapseSessionState SessionState) CreateOptions()
    {
        CollapseSessionState sessionState = new();
        ChatReducer reducer = new(
            new CompactionOptions
            {
                ModelContextWindowTokens = 100_000,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                CollapseProactiveThresholdPercentage = 1,
                CollapseBlockingThresholdPercentage = 100,
                Mode = CompactionMode.ContextCollapse,
            },
            new StaticSummaryPromptProvider("summarize"),
            new RecordingSummarizer());

        return (new AgentCompactionOptions
        {
            Reducer = reducer,
            CollapseController = new CollapseController(reducer, sessionState: sessionState),
        }, sessionState);
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

    private sealed class RecordingSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult("<summary>Current objective\nCompleted work\nNext steps</summary>");
    }

    private sealed class ScriptedAgent(
        Func<IReadOnlyList<ChatMessage>, AgentSession?, CancellationToken, Task<AgentResponse>> runAsync) : AIAgent
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
            CancellationToken cancellationToken = default) =>
            runAsync([.. messages], session, cancellationToken);

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class TestSession : AgentSession;
}
