using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Agents.Common.TokenUsage;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Create_WithConfiguredModelIdentity_PropagatesItToDefaultChatOptions()
    {
        RecordingChatClient innerChatClient = new();
        AgentBuilderService service = new(CreateCompactionOptions());

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            ChatClientIdentity.Attach(innerChatClient, "model-a"),
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [],
            EventScope: null));

        _ = await result.Agent.RunAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("model-a", innerChatClient.LastOptions?.ModelId);
    }

    [TestMethod]
    public async Task Create_UsesFrameworkFunctionLoop_AndCompactsEachProviderRequest()
    {
        FunctionLoopRecordingChatClient chatClient = new();
        RecordingSummarizer summarizer = new();
        AgentBuilderService service = new(CreateCompactionOptions(
            modelContextWindowTokens: 100,
            summarizer: summarizer));
        int toolInvocationCount = 0;
        AITool tool = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref toolInvocationCount);
                return "tool result";
            },
            "TestTool",
            "Test tool.",
            serializerOptions: null);

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            chatClient,
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [tool],
            EventScope: null));

        AgentResponse response = await result.Agent.RunAsync(
            CreateLargeHistory(),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("done", response.Text);
        Assert.AreEqual(1, toolInvocationCount);
        Assert.AreEqual(2, summarizer.CallCount);
        Assert.HasCount(2, chatClient.Requests);
        Assert.IsTrue(chatClient.Requests.All(ContainsSummaryCheckpoint));
        Assert.IsFalse(ContainsFunctionResult(chatClient.Requests[0]));
        Assert.IsTrue(ContainsFunctionResult(chatClient.Requests[1]));
    }

    [TestMethod]
    public async Task Create_WithEventScope_UsesStreamingFunctionLoopAndCompactsEachProviderRequest()
    {
        FunctionLoopRecordingChatClient chatClient = new();
        RecordingSummarizer summarizer = new();
        RecordingAgentEventScope eventScope = new();
        AgentBuilderService service = new(CreateCompactionOptions(
            modelContextWindowTokens: 100,
            summarizer: summarizer));
        int toolInvocationCount = 0;
        AITool tool = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref toolInvocationCount);
                return "tool result";
            },
            "TestTool",
            "Test tool.",
            serializerOptions: null);

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            chatClient,
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [tool],
            eventScope));

        List<AgentResponseUpdate> updates = [];
        await foreach (AgentResponseUpdate update in result.Agent.RunStreamingAsync(
            CreateLargeHistory(),
            cancellationToken: TestContext.CancellationToken))
        {
            updates.Add(update);
        }

        Assert.IsTrue(updates.Any(update => update.Text == "done"));
        Assert.AreEqual(1, toolInvocationCount);
        Assert.AreEqual(2, summarizer.CallCount);
        Assert.AreEqual(2, chatClient.StreamingCallCount);
        Assert.HasCount(2, chatClient.Requests);
        Assert.IsTrue(chatClient.Requests.All(ContainsSummaryCheckpoint));
        Assert.IsTrue(ContainsFunctionResult(chatClient.Requests[1]));
        CollectionAssert.AreEqual(
            new[]
            {
                "tool-start:tool-1:TestTool",
                "tool-complete:tool-1",
                "assistant:done",
            },
            eventScope.Events.ToArray());
    }

    [TestMethod]
    public void Create_WithLoggerFactory_PropagatesItToFrameworkFunctionInvoker()
    {
        RecordingLoggerFactory loggerFactory = new();
        AgentBuilderService service = new(CreateCompactionOptions(), loggerFactory);
        AITool tool = AIFunctionFactory.Create(() => true, "TestTool", "Test tool.", serializerOptions: null);

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            new RecordingChatClient(),
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [tool],
            EventScope: null));

        Assert.IsNotNull(result.Agent.GetService<FunctionInvokingChatClient>());
        CollectionAssert.Contains(loggerFactory.Categories, typeof(FunctionInvokingChatClient).FullName);
    }

    [TestMethod]
    public async Task Create_WithLoggerFactory_ReturnsNullForMissingOptionalKeyedService()
    {
        FunctionLoopRecordingChatClient chatClient = new();
        AgentBuilderService service = new(CreateCompactionOptions(), NullLoggerFactory.Instance);
        string? lookupResult = null;
        Func<IServiceProvider, string> readMissingKey = services =>
        {
            lookupResult = ((IKeyedServiceProvider)services)
                .GetKeyedService(typeof(string), "missing") is null
                ? "missing"
                : "unexpected";
            return lookupResult;
        };
        AITool tool = AIFunctionFactory.Create(
            readMissingKey,
            "TestTool",
            "Reads an optional keyed service.",
            serializerOptions: null);

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            chatClient,
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [tool],
            EventScope: null));

        _ = await result.Agent.RunAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("missing", lookupResult);
    }

    [TestMethod]
    public async Task Create_WithServiceProvider_ForwardsRegularAndKeyedServices()
    {
        FunctionLoopRecordingChatClient chatClient = new();
        AgentBuilderService service = new(
            CreateCompactionOptions(),
            NullLoggerFactory.Instance,
            new ForwardingServiceProvider());
        string? lookupResult = null;
        Func<IServiceProvider, string> readForwardedServices = services =>
        {
            object? regularService = services.GetService(typeof(string));
            object? keyedService = ((IKeyedServiceProvider)services)
                .GetKeyedService(typeof(string), "known");
            lookupResult = $"{regularService}:{keyedService}";
            return lookupResult;
        };
        AITool tool = AIFunctionFactory.Create(
            readForwardedServices,
            "TestTool",
            "Reads regular and keyed services.",
            serializerOptions: null);

        AgentCreationResult result = service.Create(new AgentBuildRequest(
            chatClient,
            "system prompt",
            "Test Agent",
            "Test agent description.",
            [tool],
            EventScope: null));

        _ = await result.Agent.RunAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("regular:keyed", lookupResult);
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

    private static AgentCompactionOptions CreateCompactionOptions(
        long modelContextWindowTokens = 100_000,
        ISummarizer? summarizer = null) =>
        new()
        {
            Reducer = new ChatReducer(
                new CompactionOptions
                {
                    ModelContextWindowTokens = modelContextWindowTokens,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    PreservedTailMaxTokens = 10_000,
                },
                new StaticSummaryPromptProvider("Summarize."),
                summarizer ?? new StaticSummarizer()),
        };

    private static ChatMessage[] CreateLargeHistory() => Enumerable.Range(0, 10)
        .Select(index => new ChatMessage(ChatRole.User, $"history {index}: {new string('x', 10_000)}"))
        .ToArray();

    private static bool ContainsSummaryCheckpoint(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(message =>
            message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true);

    private static bool ContainsFunctionResult(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(message => message.Contents.OfType<FunctionResultContent>().Any());

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

    private sealed class RecordingSummarizer : ISummarizer
    {
        public int CallCount { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken) =>
            IncrementAndReturnSummary();

        private ValueTask<string> IncrementAndReturnSummary()
        {
            CallCount++;
            return ValueTask.FromResult(
                """
                <summary>
                Current objective
                Completed work
                Next steps
                </summary>
                """);
        }
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

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<string> Categories { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            Categories.Add(categoryName);
            return NullLogger.Instance;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ForwardingServiceProvider : IServiceProvider, IKeyedServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(string) ? "regular" : null;

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            serviceType == typeof(string) && Equals(serviceKey, "known") ? "keyed" : null;

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            GetKeyedService(serviceType, serviceKey)
            ?? throw new InvalidOperationException("The requested keyed service is not registered.");
    }

    private sealed class FunctionLoopRecordingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public int StreamingCallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            ChatMessage response = Requests.Count == 1
                ? new(ChatRole.Assistant,
                [new FunctionCallContent("tool-1", "TestTool", new Dictionary<string, object?>())])
                : new(ChatRole.Assistant, "done");
            return Task.FromResult(new ChatResponse(response));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamingCallCount++;
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
