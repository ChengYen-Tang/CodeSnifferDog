using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core;

[TestClass]
public sealed class OperationalContextChatReducerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ReduceAsync_DoesNotCompact_WhenUsageIsBelowThreshold()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(100),
            threshold: 200);

        ChatMessage[] messages =
        [
            new(ChatRole.System, "system"),
            new(ChatRole.User, "user"),
        ];

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(messages, TestContext.CancellationToken)];

        Assert.HasCount(2, reduced);
        Assert.AreEqual(0, summarizer.CallCount);
        CollectionAssert.AreEqual(messages, reduced);
    }

    [TestMethod]
    public async Task ReduceAsync_ReplacesHistory_WithSingleSummaryCheckpoint_WhenThresholdIsExceeded()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200);

        ChatMessage[] reduced =
        [
            .. await reducer.ReduceAsync(
                [
                    new ChatMessage(ChatRole.System, "system-1"),
                    new ChatMessage(ChatRole.User, "user-1"),
                    new ChatMessage(ChatRole.Assistant, "assistant-1"),
                    new ChatMessage(ChatRole.User, "user-2"),
                ],
                TestContext.CancellationToken),
        ];

        Assert.HasCount(1, reduced);
        Assert.AreEqual(ChatRole.Assistant, reduced[0].Role);
        Assert.IsTrue(reduced[0].Text?.StartsWith("Operational summary checkpoint", StringComparison.Ordinal) ?? false);
        Assert.AreEqual(
            OperationalContextCompactionArtifactMetadata.SummaryArtifactKind,
            reduced[0].AdditionalProperties![OperationalContextCompactionArtifactMetadata.ArtifactKindKey]);
        bool isCompactionSummary = reduced[0].AdditionalProperties![OperationalContextCompactionArtifactMetadata.IsCompactionSummaryKey] is true;
        Assert.IsTrue(isCompactionSummary);
        Assert.AreEqual(1, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummaryIsMissingRequiredFragments()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new RecordingSummarizer("<summary>only partial summary</summary>"),
            new FixedUsageProvider(300),
            threshold: 200);

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummaryDoesNotContainSummaryTag()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new RecordingSummarizer("Current objective\nCompleted work\nNext steps"),
            new FixedUsageProvider(300),
            threshold: 200);

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceAsync_AddsFixedSummaryContractToPrompt()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200);

        await reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken);

        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Return text only.", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Do not call tools.", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("<summary>...</summary>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummarizerFails()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new ThrowingSummarizer(),
            new FixedUsageProvider(300),
            threshold: 200);

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceReactiveAsync_Throws_WhenSummarizerFails()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new ThrowingSummarizer(),
            new FixedUsageProvider(10),
            threshold: 20_000);

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceReactiveAsync_BypassesAutomaticThreshold()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(10),
            threshold: 20_000);

        ChatMessage[] reduced = [.. await reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken)];

        Assert.AreEqual(1, summarizer.CallCount);
        Assert.HasCount(1, reduced);
    }

    [TestMethod]
    public async Task ReduceAsync_UsesContextWindowThreshold_WhenUsageProvidesWindow()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(usedTokens: 9_000, contextWindowTokens: 10_000),
            threshold: 20_000,
            contextWindowBufferTokens: 500,
            summaryReservedOutputTokens: 600);

        await reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken);

        Assert.AreEqual(1, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ReduceAsync_RunsHooksAndCleanup_OnSuccessfulCompaction()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        RecordingHook hook = new();
        RecordingCleanupHandler cleanupHandler = new();
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200,
            hooks: [hook],
            cleanupHandlers: [cleanupHandler]);

        await reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken);

        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.AreEqual(1, cleanupHandler.CallCount);
        Assert.AreEqual(OperationalContextCompactionReason.AutomaticThreshold, hook.LastReason);
        Assert.AreEqual(OperationalContextCompactionReason.AutomaticThreshold, cleanupHandler.LastReason);
    }

    private static OperationalContextChatReducer CreateReducer(
        IOperationalContextCompactionSummarizer summarizer,
        IOperationalContextCompactionUsageProvider usageProvider,
        int threshold,
        long contextWindowBufferTokens = 8_192,
        long summaryReservedOutputTokens = 4_096,
        IEnumerable<IOperationalContextCompactionHook>? hooks = null,
        IEnumerable<IOperationalContextCompactionCleanupHandler>? cleanupHandlers = null) => new(
            new OperationalContextCompactionOptions
            {
                ContextTokenThreshold = threshold,
                ContextWindowBufferTokens = contextWindowBufferTokens,
                SummaryReservedOutputTokens = summaryReservedOutputTokens,
            },
            new StaticOperationalContextSummaryPromptProvider("summarize the current run"),
            summarizer,
            usageProvider,
            hooks: hooks,
            cleanupHandlers: cleanupHandlers);

    private sealed class FixedUsageProvider(long usedTokens, long? contextWindowTokens = null) : IOperationalContextCompactionUsageProvider
    {
        public ValueTask<OperationalContextCompactionUsage?> GetUsageAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken) => ValueTask.FromResult<OperationalContextCompactionUsage?>(new OperationalContextCompactionUsage
            {
                UsedTokens = usedTokens,
                ContextWindowTokens = contextWindowTokens,
            });
    }

    private sealed class RecordingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        public int CallCount { get; private set; }

        public string? LastSummaryPrompt { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastSummaryPrompt = summaryPrompt;
            return ValueTask.FromResult(response);
        }
    }

    private sealed class ThrowingSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class RecordingHook : IOperationalContextCompactionHook
    {
        public int BeforeCallCount { get; private set; }

        public int AfterCallCount { get; private set; }

        public OperationalContextCompactionReason? LastReason { get; private set; }

        public ValueTask OnBeforeCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken)
        {
            BeforeCallCount++;
            LastReason = reason;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnAfterCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken)
        {
            AfterCallCount++;
            LastReason = reason;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCleanupHandler : IOperationalContextCompactionCleanupHandler
    {
        public int CallCount { get; private set; }

        public OperationalContextCompactionReason? LastReason { get; private set; }

        public ValueTask CleanupAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastReason = reason;
            return ValueTask.CompletedTask;
        }
    }
}
