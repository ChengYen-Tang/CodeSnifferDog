using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Workflows.Common;

[TestClass]
public sealed class WorkflowAgentRunServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_Success_PublishesRunningThenCompletedAndReturnsUpdatedState()
    {
        RecordingAgentEventScope eventScope = new();
        List<ChatMessage> messages = [new(ChatRole.User, "scan")];
        AIAgent agent = CreateStreamingAgent(
            eventScope,
        [
            new AgentResponseUpdate(ChatRole.Assistant, "done"),
        ]);

        (Result result, int publishedMessageCount, AIAgent returnedAgent) =
            await WorkflowAgentRunService.RunAsync(
                agent,
                () => throw new InvalidOperationException("Factory should not be called."),
                static _ => new AttemptState(),
                static state => state.Restored = true,
                messages,
                eventScope,
                publishedMessageCount: 0,
                timeout: TimeSpan.FromSeconds(5),
                maxConsecutiveFailures: 1,
                TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(messages.Count, publishedMessageCount);
        Assert.AreSame(agent, returnedAgent);
        CollectionAssert.AreEqual(
            new[]
            {
                "status:Running",
                "user:scan",
                "assistant:done",
                "status:Completed",
            },
            eventScope.Events.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_Failure_PublishesRunningThenDegradedAndRestoresAttempt()
    {
        RecordingAgentEventScope eventScope = new();
        AttemptState? preparedState = null;
        AIAgent replacementAgent = CreateStreamingAgent(eventScope, []);

        (Result result, _, AIAgent returnedAgent) =
            await WorkflowAgentRunService.RunAsync(
                CreateThrowingAgent(eventScope, new InvalidOperationException("model failed")),
                () => replacementAgent,
                static _ => new AttemptState(),
                state =>
                {
                    state.Restored = true;
                    preparedState = state;
                },
                [new ChatMessage(ChatRole.User, "scan")],
                eventScope,
                publishedMessageCount: 0,
                timeout: TimeSpan.FromSeconds(5),
                maxConsecutiveFailures: 1,
                TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsNotNull(preparedState);
        Assert.IsTrue(preparedState.Restored);
        Assert.AreSame(replacementAgent, returnedAgent);
        CollectionAssert.AreEqual(
            new[]
            {
                "status:Running",
                "user:scan",
                "clear",
                "status:Degraded",
            },
            eventScope.Events.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_AllowsZeroMaxConsecutiveFailures_AsUnlimited()
    {
        RecordingAgentEventScope eventScope = new();
        List<ChatMessage> messages = [new(ChatRole.User, "scan")];
        AIAgent agent = CreateStreamingAgent(
            eventScope,
        [
            new AgentResponseUpdate(ChatRole.Assistant, "done"),
        ]);

        (Result result, _, _) = await WorkflowAgentRunService.RunAsync(
            agent,
            () => throw new InvalidOperationException("Factory should not be called."),
            static _ => new AttemptState(),
            static state => state.Restored = true,
            messages,
            eventScope,
            publishedMessageCount: 0,
            timeout: TimeSpan.FromSeconds(5),
            maxConsecutiveFailures: 0,
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.Contains(eventScope.Events, "status:Completed");
    }

    [TestMethod]
    public async Task RunAsync_Cancellation_PropagatesWithoutPublishingTerminalStatus()
    {
        RecordingAgentEventScope eventScope = new();
        using CancellationTokenSource cancellationTokenSource = new();
        AttemptState? preparedState = null;

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            WorkflowAgentRunService.RunAsync(
                CreateCancellingAgent(eventScope, cancellationTokenSource),
                () => throw new InvalidOperationException("Factory should not be called."),
                static _ => new AttemptState(),
                state =>
                {
                    state.Restored = true;
                    preparedState = state;
                },
                [new ChatMessage(ChatRole.User, "scan")],
                eventScope,
                publishedMessageCount: 0,
                timeout: TimeSpan.FromSeconds(5),
                maxConsecutiveFailures: 1,
                cancellationTokenSource.Token));

        Assert.IsNotNull(preparedState);
        Assert.IsTrue(preparedState.Restored);
        CollectionAssert.AreEqual(
            new[]
            {
                "status:Running",
                "user:scan",
            },
            eventScope.Events.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_Retry_PreservesLogicalRunStateAcrossRecreatedAgents()
    {
        RecordingAgentEventScope eventScope = new();
        object stateKey = new();
        List<object> observedStates = [];
        AIAgent succeedingAgent = new LogicalRunStateAgent(stateKey, observedStates, shouldThrow: false);

        (Result result, _, _) = await WorkflowAgentRunService.RunAsync(
            new LogicalRunStateAgent(stateKey, observedStates, shouldThrow: true),
            () => succeedingAgent,
            static _ => new AttemptState(),
            static state => state.Restored = true,
            [new ChatMessage(ChatRole.User, "scan")],
            eventScope,
            publishedMessageCount: 0,
            timeout: TimeSpan.FromSeconds(5),
            maxConsecutiveFailures: 2,
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(2, observedStates);
        Assert.AreSame(observedStates[0], observedStates[1]);
    }

    private static AIAgent CreateStreamingAgent(
        IAgentEventScope eventScope,
        IReadOnlyList<AgentResponseUpdate> updates) =>
        new AIAgentBuilder(new TestAgent(updates))
            .UseAgentTranscriptEvents(eventScope)
            .Build();

    private static AIAgent CreateThrowingAgent(IAgentEventScope eventScope, Exception exception) =>
        new AIAgentBuilder(new ThrowingAgent(exception))
            .UseAgentTranscriptEvents(eventScope)
            .Build();

    private static AIAgent CreateCancellingAgent(
        IAgentEventScope eventScope,
        CancellationTokenSource cancellationTokenSource) =>
        new AIAgentBuilder(new CancellingAgent(cancellationTokenSource))
            .UseAgentTranscriptEvents(eventScope)
            .Build();

    private sealed class AttemptState
    {
        public bool Restored { get; set; }
    }

    private sealed class RecordingAgentEventScope : IAgentEventScope
    {
        public List<string> Events { get; } = [];

        public string GroupKey => "group";

        public string AgentKey => "agent";

        public ValueTask PublishCreatedAsync(
            string displayName,
            string systemPrompt,
            string initialStatus,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default)
        {
            Events.Add($"status:{status}");
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            Events.Add($"user:{message}");
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAssistantMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            Events.Add($"assistant:{message}");
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishToolCallStartedAsync(
            string toolCallId,
            string toolName,
            string? arguments,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishToolCallCompletedAsync(
            string toolCallId,
            string? result,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishTranscriptClearedAsync(
            DateTimeOffset clearAfterUtc,
            CancellationToken cancellationToken = default)
        {
            Events.Add("clear");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestAgent(IReadOnlyList<AgentResponseUpdate> updates) : AIAgent
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
            throw new NotSupportedException();

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (AgentResponseUpdate update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return update;
            }
        }
    }

    private sealed class ThrowingAgent(Exception exception) : AIAgent
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
            throw exception;

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw exception;
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class LogicalRunStateAgent(
        object stateKey,
        List<object> observedStates,
        bool shouldThrow) : AIAgent
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
            CancellationToken cancellationToken = default)
        {
            object? state = AgentRunAttemptContext.GetOrCreateLogicalRunState(stateKey, static () => new object());
            Assert.IsNotNull(state);
            observedStates.Add(state);

            if (shouldThrow)
                throw new InvalidOperationException("first attempt failed");

            return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class CancellingAgent(CancellationTokenSource cancellationTokenSource) : AIAgent
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
            CancellationToken cancellationToken = default)
        {
            cancellationTokenSource.Cancel();
            throw new OperationCanceledException(cancellationTokenSource.Token);
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationTokenSource.Cancel();
            throw new OperationCanceledException(cancellationTokenSource.Token);
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class TestSession : AgentSession;
}
