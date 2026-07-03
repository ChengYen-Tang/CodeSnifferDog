using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Workflows.Common;

[TestClass]
public sealed class WorkflowAgentLifecycleTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CreateAndPublishAsync_Success_PublishesCreatedWithWaitingStatus()
    {
        RecordingAgentEventScope eventScope = new();
        AgentCreationResult creationResult = CreateAgentCreationResult("system prompt");

        Result<AgentCreationResult> result = await WorkflowAgentLifecycle.CreateAndPublishAsync(
            () => creationResult,
            "Test Agent",
            eventScope,
            "Display Name",
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(creationResult, result.Value);
        CollectionAssert.AreEqual(
            new[] { "created:Display Name:system prompt:Waiting" },
            eventScope.Events.ToArray());
    }

    [TestMethod]
    public async Task CreateAndPublishAsync_FactoryException_ReturnsExistingCreationFailure()
    {
        RecordingAgentEventScope eventScope = new();

        Result<AgentCreationResult> result = await WorkflowAgentLifecycle.CreateAndPublishAsync(
            () => throw new InvalidOperationException("boom"),
            "Test Agent",
            eventScope,
            "Display Name",
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.Contains("Failed to create Test Agent:", result.Errors[0].Message);
        Assert.IsEmpty(eventScope.Events);
    }

    [TestMethod]
    public async Task CreateAndPublishAsync_PublishCancellation_Propagates()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        RecordingAgentEventScope eventScope = new();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            WorkflowAgentLifecycle.CreateAndPublishAsync(
                () => CreateAgentCreationResult("system prompt"),
                "Test Agent",
                eventScope,
                "Display Name",
                cancellationTokenSource.Token));
    }

    private static AgentCreationResult CreateAgentCreationResult(string systemPrompt) =>
        new()
        {
            Agent = new TestAgent(),
            SystemPrompt = systemPrompt,
        };

    private sealed class RecordingAgentEventScope : IAgentEventScope
    {
        public List<string> Events { get; } = [];

        public string GroupKey => "group";

        public string AgentKey => "agent";

        public ValueTask PublishCreatedAsync(
            string displayName,
            string systemPrompt,
            string initialStatus,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add($"created:{displayName}:{systemPrompt}:{initialStatus}");
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishAssistantMessageAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

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
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new AgentResponseUpdate(ChatRole.Assistant, "ok");
        }
    }

    private sealed class TestSession : AgentSession;
}
