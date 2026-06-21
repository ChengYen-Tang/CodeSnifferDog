using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;

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
            OperationalContextCompactionReason.AutomaticThreshold,
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
            OperationalContextCompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.AreEqual(1, cleanupHandler.CallCount);
    }

    [TestMethod]
    public async Task CompactAsync_Throws_WhenSummaryDoesNotContainSummaryTag()
    {
        ReductionPipeline pipeline = CreatePipeline(new RecordingSummarizer("Current objective\nCompleted work\nNext steps"));

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => pipeline.CompactAsync(
                [new ChatMessage(ChatRole.User, new string('x', 1_000))],
                OperationalContextCompactionReason.AutomaticThreshold,
                TestContext.CancellationToken));
    }

    private static ReductionPipeline CreatePipeline(
        IOperationalContextCompactionSummarizer summarizer,
        IEnumerable<IOperationalContextCompactionHook>? hooks = null,
        IEnumerable<IOperationalContextCompactionCleanupHandler>? cleanupHandlers = null) => new(
            new OperationalContextCompactionOptions
            {
                ModelContextWindowTokens = 3,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                PreservedTailMaxTokens = 10_000,
            },
            new StaticOperationalContextSummaryPromptProvider("summarize the current run"),
            summarizer,
            artifactsProvider: null,
            hooks,
            cleanupHandlers);

    private sealed class RecordingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        public string? LastSummaryPrompt { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken)
        {
            LastSummaryPrompt = summaryPrompt;
            return ValueTask.FromResult(response);
        }
    }

    private sealed class RecordingHook : IOperationalContextCompactionHook
    {
        public int BeforeCallCount { get; private set; }

        public int AfterCallCount { get; private set; }

        public ValueTask OnBeforeCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken)
        {
            BeforeCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnAfterCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken)
        {
            AfterCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCleanupHandler : IOperationalContextCompactionCleanupHandler
    {
        public int CallCount { get; private set; }

        public ValueTask CleanupAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
