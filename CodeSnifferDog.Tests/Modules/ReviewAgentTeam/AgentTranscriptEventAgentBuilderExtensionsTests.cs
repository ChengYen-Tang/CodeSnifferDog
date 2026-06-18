using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Modules.ReviewAgentTeam;

[TestClass]
public sealed class AgentTranscriptEventAgentBuilderExtensionsTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_PublishesStreamingTranscriptEvents_AndMarksResponse()
    {
        RecordingAgentEventScope eventScope = new();
        TestAgent innerAgent = new(
        [
            new AgentResponseUpdate(ChatRole.Assistant, "I will inspect Program.cs.")
            {
                MessageId = "assistant-1",
            },
            new AgentResponseUpdate(ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "call-1",
                    "RunShellCommand",
                    new Dictionary<string, object?> { ["command"] = "rg Program.cs" }),
            ])
            {
                MessageId = "assistant-1",
            },
            new AgentResponseUpdate(ChatRole.Tool,
            [
                new FunctionResultContent("call-1", "Program.cs"),
            ])
            {
                MessageId = "tool-1",
            },
            new AgentResponseUpdate(ChatRole.Assistant, "Done.")
            {
                MessageId = "assistant-2",
            },
        ]);

        AIAgent agent = new AIAgentBuilder(innerAgent)
            .UseAgentTranscriptEvents(eventScope)
            .Build();

        AgentResponse response = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, "scan")],
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(AgentTranscriptEventAgentBuilderExtensions.HasPublishedTranscriptEvents(response));
        CollectionAssert.AreEqual(
            new[]
            {
                "assistant:I will inspect Program.cs.",
                "tool-start:call-1:RunShellCommand",
                "tool-complete:call-1",
                "assistant:Done.",
            },
            eventScope.Events.ToArray());
        Assert.IsTrue(innerAgent.StreamingWasUsed);
        Assert.IsFalse(innerAgent.RunCoreWasUsed);
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
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

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
            CancellationToken cancellationToken = default)
        {
            Events.Add($"tool-start:{toolCallId}:{toolName}");
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishToolCallCompletedAsync(
            string toolCallId,
            string? result,
            CancellationToken cancellationToken = default)
        {
            Events.Add($"tool-complete:{toolCallId}");
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PublishTranscriptClearedAsync(
            DateTimeOffset clearAfterUtc,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestAgent(IReadOnlyList<AgentResponseUpdate> updates) : AIAgent
    {
        public bool RunCoreWasUsed { get; private set; }

        public bool StreamingWasUsed { get; private set; }

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
            RunCoreWasUsed = true;
            throw new NotSupportedException();
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamingWasUsed = true;

            foreach (AgentResponseUpdate update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return update;
            }
        }
    }

    private sealed class TestSession : AgentSession;
}
