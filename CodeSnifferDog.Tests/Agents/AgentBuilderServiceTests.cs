using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using TranscriptAgentBuilderExtensions = CodeSnifferDog.Modules.ReviewAgentTeam.Transcript.AgentBuilderExtensions;

namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentBuilderServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task Create_ReturnsAgentCreationResult_WithSystemPromptAndRunnableAgent()
    {
        RecordingChatClient chatClient = new();
        AgentBuilderService service = new(CreateCompactionOptions());
        AITool tool = AIFunctionFactory.Create(() => true, "TestTool", "Test tool.", serializerOptions: null);

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            chatClient,
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [tool],
            EventScope: null));

        Assert.AreEqual("system prompt", result.SystemPrompt);
        Assert.IsNotNull(result.Agent);

        AgentResponse response = await result.Agent.RunAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("ok", response.Text);
        Assert.AreEqual("system prompt", chatClient.LastOptions?.Instructions);
        CollectionAssert.AreEqual(new[] { "TestTool" }, chatClient.LastOptions?.Tools?.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task Create_WithEventScope_PublishesTranscriptEventsThroughScope()
    {
        StreamingRecordingChatClient chatClient = new();
        RecordingAgentEventScope eventScope = new();
        AgentBuilderService service = new(CreateCompactionOptions());

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            chatClient,
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [],
            eventScope));

        AgentResponse response = await result.Agent.RunAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(TranscriptAgentBuilderExtensions.HasPublishedTranscriptEvents(response));
        CollectionAssert.AreEqual(
            new[]
            {
                "assistant:I will inspect.",
                "tool-start:call-1:RunShellCommand",
                "tool-complete:call-1",
                "assistant:Done.",
            },
            eventScope.Events.ToArray());
    }

    private static AgentCompactionOptions CreateCompactionOptions() =>
        new()
        {
            Reducer = new ChatReducer(
                new CompactionOptions
                {
                    ModelContextWindowTokens = 100_000,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                },
                new StaticSummaryPromptProvider("Summarize."),
                new StaticSummarizer()),
        };

    private sealed class StaticSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                """
                Current objective
                Completed work
                Next steps
                """);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class StreamingRecordingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Transcript middleware should use streaming.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            ChatMessage[] responseMessages =
            [
                new(ChatRole.Assistant, "I will inspect."),
                new(ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        "call-1",
                        "RunShellCommand",
                        new Dictionary<string, object?> { ["Command"] = "Get-ChildItem" }),
                ]),
                new(ChatRole.Tool,
                [
                    new FunctionResultContent("call-1", "files"),
                ]),
                new(ChatRole.Assistant, "Done."),
            ];

            foreach (ChatMessage responseMessage in responseMessages)
            {
                foreach (ChatResponseUpdate update in new ChatResponse(responseMessage).ToChatResponseUpdates())
                    yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
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
}
