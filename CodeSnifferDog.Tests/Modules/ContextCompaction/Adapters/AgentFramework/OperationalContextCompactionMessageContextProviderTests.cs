using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework;

[TestClass]
public sealed class OperationalContextCompactionMessageContextProviderTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task InvokingAsync_ReturnsOriginalMessages_WhenAutomaticCompactionFails()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(new ThrowingSummarizer())));

        ChatMessage[] messages = [new(ChatRole.User, "user")];

        IEnumerable<ChatMessage> provided = await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(messages, provided.ToArray());
    }

    [TestMethod]
    public async Task InvokingAsync_StopsRetryingAutomaticCompaction_AfterCircuitBreakerTrips()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        CountingThrowingSummarizer summarizer = new();
        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(summarizer)));

        ChatMessage[] messages = [new(ChatRole.User, new string('x', 1_000))];

        for (int index = 0; index < 4; index++)
            _ = await InvokeProvideMessagesAsync(
                provider,
                new MessageAIContextProvider.InvokingContext(agent, session, messages),
                TestContext.CancellationToken);

        Assert.AreEqual(3, summarizer.CallCount);
    }

    [TestMethod]
    public async Task InvokingAsync_ResetsCircuitBreakerState_AfterSuccessfulAutomaticCompaction()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        SequenceSummarizer summarizer = new(
            () => throw new InvalidOperationException("boom"),
            () => "<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(summarizer)));

        ChatMessage[] messages = [new(ChatRole.User, new string('x', 1_000))];

        _ = await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken);

        IEnumerable<ChatMessage> secondAttempt = await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken);

        Assert.IsTrue(secondAttempt.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.SummaryArtifactKind));
    }

    [TestMethod]
    public async Task InvokingAsync_DoesNotRunAutomaticCompaction_InReactiveOnlyMode()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        CountingThrowingSummarizer summarizer = new();
        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(
                summarizer,
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    Mode = OperationalContextCompactionMode.ReactiveOnly,
                })));

        ChatMessage[] messages = [new(ChatRole.User, new string('x', 1_000))];

        IEnumerable<ChatMessage> provided = await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken);

        Assert.AreEqual(0, summarizer.CallCount);
        CollectionAssert.AreEqual(messages, provided.ToArray());
    }

    [TestMethod]
    public async Task InvokingAsync_ProjectsCollapseCommits_InContextCollapseMode()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        OperationalContextCollapseSessionState collapseSessionState = new();
        collapseSessionState.StageCollapseSpan(
            session,
            new OperationalContextCompactionResult
            {
                WasCompacted = true,
                PreservedSystemMessages = [],
                BoundaryMessage = new ChatMessage(ChatRole.System, "Operational compact boundary")
                {
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        [OperationalContextCompactionArtifactMetadata.BoundarySummaryKey] = "collapsed summary",
                    },
                },
                SummaryMessage = new ChatMessage(ChatRole.Assistant, "Operational summary checkpoint\n\ncollapsed summary"),
                ContinuityStateMessage = new ChatMessage(ChatRole.System, "Operational continuity state"),
                ContinuityState = new OperationalContextContinuityState
                {
                    CurrentObjective = "objective",
                    CompletedWork = "completed",
                    NextSteps = "next",
                    CriticalContext = "context",
                },
                MessagesToKeep = [],
                MessageReferences = [],
                ArchivedMessageReferences =
                [
                    new OperationalContextCompactionMessageReference
                    {
                        MessageIndex = 0,
                        MessageId = "message-0",
                        Role = ChatRole.User,
                        Text = "user",
                    },
                ],
                AttachmentMessages = [],
                HookResultMessages = [],
            },
            OperationalContextCompactionReason.Reactive);
        string collapseId = collapseSessionState.Get(session).Snapshot.LastStagedCollapseId!;
        collapseSessionState.CommitStagedSpan(session, collapseId);
        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(
                new SequenceSummarizer(() => "<summary>Current objective\nCompleted work\nNext steps</summary>"),
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    Mode = OperationalContextCompactionMode.ContextCollapse,
                }),
                new OperationalContextCollapseProjectionBuilder(),
                collapseSessionState));

        ChatMessage[] messages = [new(ChatRole.User, "user")];

        IEnumerable<ChatMessage> provided = await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken);

        ChatMessage[] providedMessages = [.. provided];
        Assert.IsTrue(providedMessages.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.CollapseProjectionArtifactKind));
        Assert.IsTrue(providedMessages.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.ContinuityArtifactKind));
        Assert.IsFalse(providedMessages.Any(message => string.Equals(message.Text, "user", StringComparison.Ordinal)));

        OperationalContextCollapseState state = collapseSessionState.Get(session);
        Assert.HasCount(1, state.Snapshot.ProjectedCollapseIds);
        Assert.IsNotNull(state.Snapshot.LastProjectedAtUtc);
        CollectionAssert.AreEqual(new string[] { collapseId }, state.Snapshot.ProjectedCollapseIds.ToArray());
        Assert.AreEqual($"collapse-projection-{collapseId}", providedMessages.First(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.CollapseProjectionArtifactKind)
            .AdditionalProperties![OperationalContextCompactionArtifactMetadata.MessageIdentityKey]);
    }

    [TestMethod]
    public async Task InvokingAsync_ProjectsAllCommittedCollapseSpans_InContextCollapseMode()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        OperationalContextCollapseSessionState collapseSessionState = new();

        for (int collapseIndex = 0; collapseIndex < 4; collapseIndex++)
        {
            collapseSessionState.StageCollapseSpan(
                session,
                new OperationalContextCompactionResult
                {
                    WasCompacted = true,
                    PreservedSystemMessages = [],
                    BoundaryMessage = new ChatMessage(ChatRole.System, "Operational compact boundary")
                    {
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            [OperationalContextCompactionArtifactMetadata.BoundarySummaryKey] = $"collapsed summary {collapseIndex}",
                        },
                    },
                    SummaryMessage = new ChatMessage(ChatRole.Assistant, $"Operational summary checkpoint\n\ncollapsed summary {collapseIndex}"),
                    ContinuityStateMessage = new ChatMessage(ChatRole.System, "Operational continuity state"),
                    ContinuityState = new OperationalContextContinuityState
                    {
                        CurrentObjective = $"objective-{collapseIndex}",
                        CompletedWork = $"completed-{collapseIndex}",
                        NextSteps = $"next-{collapseIndex}",
                        CriticalContext = $"context-{collapseIndex}",
                    },
                    MessagesToKeep = [],
                    MessageReferences = [],
                    ArchivedMessageReferences =
                    [
                        new OperationalContextCompactionMessageReference
                        {
                            MessageIndex = collapseIndex,
                            MessageId = $"message-{collapseIndex}",
                            Role = ChatRole.User,
                            Text = $"user-{collapseIndex}",
                        },
                    ],
                    AttachmentMessages = [],
                    HookResultMessages = [],
                },
                OperationalContextCompactionReason.Reactive);
            collapseSessionState.CommitStagedSpan(session, collapseSessionState.Get(session).Snapshot.LastStagedCollapseId!);
        }

        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(
                new SequenceSummarizer(() => "<summary>Current objective\nCompleted work\nNext steps</summary>"),
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    Mode = OperationalContextCompactionMode.ContextCollapse,
                }),
                new OperationalContextCollapseProjectionBuilder(),
                collapseSessionState));

        ChatMessage[] messages =
        [
            CreateMessage(ChatRole.User, "user-0", "message-0"),
            CreateMessage(ChatRole.User, "user-1", "message-1"),
            CreateMessage(ChatRole.User, "user-2", "message-2"),
            CreateMessage(ChatRole.User, "user-3", "message-3"),
        ];

        ChatMessage[] provided = [.. await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken)];

        Assert.HasCount(4, provided.Where(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.CollapseProjectionArtifactKind));

        OperationalContextCollapseState state = collapseSessionState.Get(session);
        Assert.HasCount(4, state.Snapshot.ProjectedCollapseIds);
    }

    [TestMethod]
    public async Task InvokingAsync_CommitsProactiveCollapse_InContextCollapseMode_WhenBlockingThresholdIsExceeded()
    {
        TestAgent agent = new();
        AgentSession session = await agent.CreateSessionAsync(TestContext.CancellationToken);
        OperationalContextCompactionMessageContextProvider provider = new(
            CreateAgentOptions(CreateReducer(
                new SequenceSummarizer(() => "<summary>Current objective\nCompleted work\nNext steps</summary>"),
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    Mode = OperationalContextCompactionMode.ContextCollapse,
                    CollapseProactiveThresholdPercentage = 10,
                })));

        ChatMessage[] messages =
        [
            new(ChatRole.User, new string('x', 1_000)),
            new(ChatRole.Assistant, "assistant"),
        ];

        ChatMessage[] provided = [.. await InvokeProvideMessagesAsync(
            provider,
            new MessageAIContextProvider.InvokingContext(agent, session, messages),
            TestContext.CancellationToken)];

        Assert.IsTrue(provided.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.CollapseProjectionArtifactKind));
        Assert.IsTrue(provided.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.ContinuityArtifactKind));

        OperationalContextCollapseState state = new OperationalContextCollapseSessionState().Get(session);
        Assert.IsEmpty(state.StagedSpans);
        Assert.HasCount(1, state.Commits);
        Assert.AreEqual(OperationalContextCompactionReason.ContextCollapseProactive.ToString(), state.Commits[0].Reason);
        Assert.IsFalse(state.Snapshot.Armed);
        Assert.IsNotNull(state.Snapshot.LastSpawnTokens);
        Assert.IsNotNull(state.Snapshot.LastArmedAtUtc);
        Assert.AreEqual(state.Commits[0].CollapseId, state.Snapshot.LastCommittedCollapseId);
    }

    private static OperationalContextCompactionOptions CreateOptions() => new()
    {
        ModelContextWindowTokens = 100,
        SummaryReservedOutputTokens = 1,
        AutoCompactBufferTokens = 1,
        PreservedTailMinTokens = 1,
        PreservedTailMinMessages = 1,
        MaxConsecutiveAutomaticFailures = 3,
    };

    private static OperationalContextChatReducer CreateReducer(
        IOperationalContextCompactionSummarizer summarizer,
        OperationalContextCompactionOptions? options = null) =>
        new(
            options ?? CreateOptions(),
            new StaticOperationalContextSummaryPromptProvider("summarize"),
            summarizer,
            new MetadataOperationalContextCompactionArtifactsProvider(options ?? CreateOptions()));

    private static OperationalContextAgentCompactionOptions CreateAgentOptions(
        OperationalContextChatReducer reducer,
        OperationalContextCollapseProjectionBuilder? projectionBuilder = null,
        OperationalContextCollapseSessionState? collapseSessionState = null) => new()
        {
            Reducer = reducer,
            CollapseController = new OperationalContextCollapseController(
                reducer,
                projectionBuilder,
                collapseSessionState),
            MessageShrinker = new OperationalContextMessageShrinker(),
            ReactiveExceptionDecider = new DefaultOperationalContextReactiveCompactionExceptionDecider(),
        };

    private static async Task<IEnumerable<ChatMessage>> InvokeProvideMessagesAsync(
        OperationalContextCompactionMessageContextProvider provider,
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken)
    {
        object? result = typeof(OperationalContextCompactionMessageContextProvider)
            .GetMethod("ProvideMessagesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(provider, [context, cancellationToken]);

        return await ((ValueTask<IEnumerable<ChatMessage>>)result!).ConfigureAwait(false);
    }

    private static ChatMessage CreateMessage(ChatRole role, string text, string messageId)
    {
        ChatMessage message = new(role, text)
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [OperationalContextCompactionArtifactMetadata.MessageIdentityKey] = messageId,
            },
        };

        return message;
    }

    private sealed class ThrowingSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class CountingThrowingSummarizer : IOperationalContextCompactionSummarizer
    {
        public int CallCount { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class SequenceSummarizer(params Func<string>[] behaviors) : IOperationalContextCompactionSummarizer
    {
        private readonly Queue<Func<string>> _behaviors = new(behaviors);

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_behaviors.Dequeue().Invoke());
    }

    private sealed class TestAgent : AIAgent
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
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class TestSession : AgentSession;
}
