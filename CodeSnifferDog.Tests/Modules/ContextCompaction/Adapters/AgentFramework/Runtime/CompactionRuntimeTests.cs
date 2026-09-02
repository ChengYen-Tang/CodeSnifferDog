using CodeSnifferDog.Agents.Common;
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
using CodeSnifferDog.Workflows.Common;

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

    [TestMethod]
    public async Task RunAsync_RetriesWhenRawProviderExceptionReportsContextWindowOverflow()
    {
        AgentCompactionOptions options = CreateStandardOptions();
        TestSession session = new();
        ChatMessage[] messages =
        [
            new(ChatRole.User, new string('x', 10_000)),
            new(ChatRole.Assistant, "recent tail"),
        ];
        List<IReadOnlyList<ChatMessage>> invocations = [];
        ScriptedAgent agent = new((currentMessages, _, _) =>
        {
            invocations.Add(currentMessages);

            if (invocations.Count == 1)
                throw new HttpRequestException("HTTP 400 context_too_large");

            return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        });

        _ = await CompactionRuntime.RunAsync(messages, session, null, agent, options, TestContext.CancellationToken);

        Assert.HasCount(2, invocations);
        Assert.IsTrue(invocations[1].Any(message =>
            message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true));
    }

    [TestMethod]
    public async Task RunAsync_DoesNotCompactTheAlreadyCompactedReactiveRetryTwice()
    {
        RecordingSummarizer summarizer = new();
        AgentCompactionOptions options = new()
        {
            Reducer = new ChatReducer(
                new CompactionOptions
                {
                    ModelContextWindowTokens = 100_000,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    PreservedTailMaxTokens = 10_000,
                },
                new StaticSummaryPromptProvider("summarize"),
                summarizer),
        };
        ContextWindowThenSuccessChatClient provider = new();
        CompactingChatClient compactingClient = new(provider, options);
        ChatMessage[] messages =
        [
            new(ChatRole.User, new string('x', 10_000)),
            new(ChatRole.Assistant, "recent tail"),
        ];
        ScriptedAgent agent = new(async (currentMessages, _, cancellationToken) =>
        {
            ChatResponse response = await compactingClient
                .GetResponseAsync(currentMessages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new AgentResponse([.. response.Messages]);
        });

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            () => CompactionRuntime.RunAsync(
                messages,
                new TestSession(),
                null,
                agent,
                options,
                TestContext.CancellationToken));

        Assert.HasCount(2, provider.Requests);
        Assert.HasCount(1, summarizer.Inputs);
        Assert.AreEqual(1, provider.Requests[1].Count(message =>
            message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true));
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

    private static AgentCompactionOptions CreateStandardOptions() => new()
    {
        Reducer = new ChatReducer(
            new CompactionOptions
            {
                ModelContextWindowTokens = 100_000,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                PreservedTailMaxTokens = 10_000,
            },
            new StaticSummaryPromptProvider("summarize"),
            new RecordingSummarizer()),
    };

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
        public List<IReadOnlyList<ChatMessage>> Inputs { get; } = [];

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken)
        {
            Inputs.Add([.. messages]);
            return ValueTask.FromResult("<summary>Current objective\nCompleted work\nNext steps</summary>");
        }
    }

    private sealed class ContextWindowThenSuccessChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            if (Requests.Count == 1)
                throw new HttpRequestException("HTTP 400 context_too_large");

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
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
