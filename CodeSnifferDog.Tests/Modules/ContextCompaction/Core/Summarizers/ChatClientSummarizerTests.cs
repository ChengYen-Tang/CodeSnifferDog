using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core.Summarizers;

[TestClass]
public sealed class ChatClientSummarizerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task SummarizeAsync_TrimsTrailingToolOnlyMessages_BeforeSummaryCall()
    {
        RecordingChatClient chatClient = new();
        ChatClientSummarizer summarizer = new(chatClient);

        await summarizer.SummarizeAsync(
            [
                new ChatMessage(ChatRole.User, "user-1"),
                new ChatMessage(ChatRole.Assistant, "assistant-1"),
                new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
                new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "ok")]),
            ],
            "summarize now",
            new CompactionOptions
            {
                ModelContextWindowTokens = 100,
            },
            TestContext.CancellationToken);

        Assert.HasCount(3, chatClient.LastMessages);
        Assert.AreEqual("user-1", chatClient.LastMessages[0].Text);
        Assert.AreEqual("assistant-1", chatClient.LastMessages[1].Text);
        Assert.AreEqual("summarize now", chatClient.LastMessages[2].Text);
    }

    [TestMethod]
    public async Task SummarizeAsync_ForwardsSummaryModelId()
    {
        RecordingChatClient chatClient = new();
        ChatClientSummarizer summarizer = new(chatClient);

        await summarizer.SummarizeAsync(
            [
                new ChatMessage(ChatRole.User, "user-1"),
            ],
            "summarize now",
            new CompactionOptions
            {
                ModelContextWindowTokens = 100,
                SummaryModelId = "summary-model",
            },
            TestContext.CancellationToken);

        Assert.AreEqual("summary-model", chatClient.LastOptions?.ModelId);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = [.. messages];
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "<summary>Current objective\nCompleted work\nNext steps</summary>")));
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
