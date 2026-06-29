using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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

    private static OperationalContextAgentCompactionOptions CreateCompactionOptions() =>
        new()
        {
            Reducer = new OperationalContextChatReducer(
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100_000,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                },
                new StaticOperationalContextSummaryPromptProvider("Summarize."),
                new StaticSummarizer()),
        };

    private sealed class StaticSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
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
}
