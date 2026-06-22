using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

[TestClass]
public sealed class AgentFrameworkCompactionRuntimeTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_CommitsNewStagedCollapse_OnSuccess()
    {
        (OperationalContextAgentCompactionOptions options, OperationalContextCollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("success");
        ScriptedAgent agent = new(async (_, currentSession, cancellationToken) =>
        {
            await StageCollapseAsync(options, sessionState, currentSession!, messages, cancellationToken);
            return new AgentResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        });

        _ = await AgentFrameworkCompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken);

        OperationalContextCollapseState state = sessionState.Get(session);
        Assert.HasCount(1, state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    [TestMethod]
    public async Task RunAsync_DiscardsNewStagedCollapse_WhenRunFailsWithoutRetry()
    {
        (OperationalContextAgentCompactionOptions options, OperationalContextCollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("failure");
        ScriptedAgent agent = new(async (_, currentSession, cancellationToken) =>
        {
            await StageCollapseAsync(options, sessionState, currentSession!, messages, cancellationToken);
            throw new OperationalContextModelInvocationException(
                OperationalContextModelInvocationFailureKind.Unknown,
                "boom");
        });

        await Assert.ThrowsExactlyAsync<OperationalContextModelInvocationException>(
            () => AgentFrameworkCompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken));

        OperationalContextCollapseState state = sessionState.Get(session);
        Assert.IsEmpty(state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    [TestMethod]
    public async Task RunAsync_PreservesOriginalException_WhenReactiveRetryMessagesAreEquivalent()
    {
        (OperationalContextAgentCompactionOptions options, _) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = [new(ChatRole.User, "single-message")];
        ScriptedAgent agent = new((_, _, _) => throw new OperationalContextModelInvocationException(
            OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
            "original"));

        OperationalContextModelInvocationException exception = await Assert.ThrowsExactlyAsync<OperationalContextModelInvocationException>(
            () => AgentFrameworkCompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken));

        Assert.AreEqual("original", exception.Message);
    }

    private static ChatMessage[] CreateMessages(string suffix) =>
    [
        new(ChatRole.User, $"user-{suffix}"),
        new(ChatRole.Assistant, $"assistant-{suffix}"),
        new(ChatRole.User, new string('x', 1_000)),
    ];

    private static (OperationalContextAgentCompactionOptions Options, OperationalContextCollapseSessionState SessionState) CreateOptions()
    {
        OperationalContextCollapseSessionState sessionState = new();
        OperationalContextChatReducer reducer = new(
            new OperationalContextCompactionOptions
            {
                ModelContextWindowTokens = 100_000,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                CollapseProactiveThresholdPercentage = 1,
                CollapseBlockingThresholdPercentage = 100,
                Mode = OperationalContextCompactionMode.ContextCollapse,
            },
            new StaticOperationalContextSummaryPromptProvider("summarize"),
            new RecordingSummarizer());

        return (new OperationalContextAgentCompactionOptions
        {
            Reducer = reducer,
            CollapseController = new OperationalContextCollapseController(reducer, sessionState: sessionState),
        }, sessionState);
    }

    private static async Task StageCollapseAsync(
        OperationalContextAgentCompactionOptions options,
        OperationalContextCollapseSessionState sessionState,
        AgentSession session,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        OperationalContextCompactionResult result = await options.Reducer
            .CompactReactiveAsync(messages, cancellationToken)
            .ConfigureAwait(false);
        sessionState.StageCollapseSpan(session, result, OperationalContextCompactionReason.ContextCollapseProactive);
    }

    private sealed class RecordingSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
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
