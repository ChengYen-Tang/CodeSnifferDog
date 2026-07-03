using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core.Reduction;

[TestClass]
public sealed class ReductionPipelineTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CompactAsync_AddsSummaryContractToPrompt()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ReductionPipeline pipeline = CreatePipeline(summarizer);

        await pipeline.CompactAsync(
            [new ChatMessage(ChatRole.User, new string('x', 1_000))],
            CompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Return text only.", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("<summary>...</summary>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CompactAsync_RunsHooksAndCleanup_OnSuccessfulCompaction()
    {
        RecordingHook hook = new();
        RecordingCleanupHandler cleanupHandler = new();
        ReductionPipeline pipeline = CreatePipeline(
            new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"),
            hooks: [hook],
            cleanupHandlers: [cleanupHandler]);

        await pipeline.CompactAsync(
            [new ChatMessage(ChatRole.User, new string('x', 1_000))],
            CompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.AreEqual(1, cleanupHandler.CallCount);
    }

    [TestMethod]
    public async Task CompactAsync_Throws_WhenSummaryDoesNotContainSummaryTag()
    {
        ReductionPipeline pipeline = CreatePipeline(new RecordingSummarizer("Current objective\nCompleted work\nNext steps"));

        await Assert.ThrowsExactlyAsync<CompactionException>(
            () => pipeline.CompactAsync(
                [new ChatMessage(ChatRole.User, new string('x', 1_000))],
                CompactionReason.AutomaticThreshold,
                TestContext.CancellationToken));
    }

    private static ReductionPipeline CreatePipeline(
        ISummarizer summarizer,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null) => new(
            new CompactionOptions
            {
                ModelContextWindowTokens = 3,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                PreservedTailMaxTokens = 10_000,
            },
            new StaticSummaryPromptProvider("summarize the current run"),
            summarizer,
            artifactsProvider: null,
            hooks,
            cleanupHandlers);

    private sealed class RecordingSummarizer(string response) : ISummarizer
    {
        public string? LastSummaryPrompt { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken)
        {
            LastSummaryPrompt = summaryPrompt;
            return ValueTask.FromResult(response);
        }
    }

    private sealed class RecordingHook : IHook
    {
        public int BeforeCallCount { get; private set; }

        public int AfterCallCount { get; private set; }

        public ValueTask OnBeforeCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            BeforeCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnAfterCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            AfterCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCleanupHandler : ICleanupHandler
    {
        public int CallCount { get; private set; }

        public ValueTask CleanupAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
