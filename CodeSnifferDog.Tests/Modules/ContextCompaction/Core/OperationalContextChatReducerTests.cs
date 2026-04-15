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

        IEnumerable<ChatMessage> reduced = await reducer.ReduceAsync(messages, TestContext.CancellationToken);

        Assert.AreEqual(2, reduced.Count());
        Assert.AreEqual(0, summarizer.CallCount);
        CollectionAssert.AreEqual(messages, reduced.ToArray());
    }

    [TestMethod]
    public async Task ReduceAsync_PreservesSystemBoundarySummaryAndTail_WhenThresholdIsExceeded()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200);

        ChatMessage[] messages =
        [
            new(ChatRole.System, "system-1"),
            new(ChatRole.User, "user-1"),
            new(ChatRole.Assistant, "assistant-boundary"),
            new(ChatRole.Assistant, "assistant-1"),
            new(ChatRole.User, "user-2"),
        ];

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(messages, TestContext.CancellationToken)];

        Assert.HasCount(5, reduced);
        Assert.AreEqual(ChatRole.System, reduced[0].Role);
        Assert.AreEqual("system-1", reduced[0].Text);
        Assert.AreEqual("Operational context boundary marker", reduced[1].Text);
        Assert.AreEqual(
            OperationalContextCompactionArtifactMetadata.BoundaryArtifactKind,
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.ArtifactKindKey]);
        Assert.AreEqual(
            "assistant-boundary",
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.BoundaryAnchorTextKey]);
        Assert.AreEqual(
            2,
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.BoundaryAnchorIndexKey]);
        Assert.AreEqual(
            "3,4",
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.PreservedTailIndexesKey]);
        Assert.AreEqual(ChatRole.Assistant, reduced[2].Role);
        Assert.Contains("Operational summary checkpoint", reduced[2].Text);
        Assert.AreEqual(
            OperationalContextCompactionArtifactMetadata.SummaryArtifactKind,
            reduced[2].AdditionalProperties![OperationalContextCompactionArtifactMetadata.ArtifactKindKey]);
        Assert.AreEqual(
            true,
            reduced[2].AdditionalProperties!.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.IsCompactionSummaryKey));
        Assert.AreEqual(
            true,
            reduced[2].AdditionalProperties!.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.HasPreservedTailKey));
        Assert.Contains("Current objective", reduced[2].Text);
        Assert.AreEqual("assistant-1", reduced[3].Text);
        Assert.AreEqual("user-2", reduced[4].Text);
        Assert.AreEqual(1, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ReduceAsync_UsesStableIndexes_WhenTextsRepeat()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200);

        ChatMessage[] messages =
        [
            new(ChatRole.System, "system-1"),
            new(ChatRole.User, "same"),
            new(ChatRole.Assistant, "same"),
            new(ChatRole.User, "same"),
            new(ChatRole.Assistant, "same"),
        ];

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(messages, TestContext.CancellationToken)];

        Assert.AreEqual(
            2,
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.BoundaryAnchorIndexKey]);
        Assert.AreEqual(
            3,
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.PreservedSegmentHeadIndexKey]);
        Assert.AreEqual(
            4,
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.PreservedSegmentTailIndexKey]);
        Assert.AreEqual(
            "3,4",
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.PreservedTailIndexesKey]);
        Assert.AreEqual(
            "same",
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.BoundaryAnchorTextKey]);
        Assert.AreEqual(
            2,
            reduced[2].AdditionalProperties![OperationalContextCompactionArtifactMetadata.MessagesToKeepCountKey]);
    }

    [TestMethod]
    public async Task ReduceAsync_DoesNotDuplicateBoundaryAndTail_WhenTailConsumesAllCandidateMessages()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200);

        ChatMessage[] messages =
        [
            new(ChatRole.System, "system-1"),
            new(ChatRole.User, "user-1"),
            new(ChatRole.Assistant, "assistant-1"),
        ];

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(messages, TestContext.CancellationToken)];

        Assert.HasCount(5, reduced);
        Assert.AreEqual("system-1", reduced[0].Text);
        Assert.AreEqual("Operational context boundary marker", reduced[1].Text);
        Assert.Contains("Operational summary checkpoint", reduced[2].Text);
        Assert.Contains("Current objective", reduced[2].Text);
        Assert.AreEqual("user-1", reduced[3].Text);
        Assert.AreEqual("assistant-1", reduced[4].Text);
        Assert.AreEqual(1, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummaryIsMissingRequiredFragments()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new RecordingSummarizer("only partial summary"),
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
        Assert.Contains("Return text only.", summarizer.LastSummaryPrompt);
        Assert.Contains("Do not call tools.", summarizer.LastSummaryPrompt);
        Assert.Contains("<summary>...</summary>", summarizer.LastSummaryPrompt);
    }

    [TestMethod]
    public async Task ReduceAsync_StopsCompacting_AfterMaxConsecutiveFailures()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new ThrowingSummarizer(),
            new FixedUsageProvider(300),
            threshold: 200);

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));

        ChatMessage[] originalMessages =
        [
            new(ChatRole.User, "user"),
            new(ChatRole.Assistant, "assistant"),
        ];

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(originalMessages, TestContext.CancellationToken)];

        CollectionAssert.AreEqual(originalMessages, reduced);
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
    public async Task ReduceReactiveAsync_BypassesAutomaticThreshold()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(usedTokens: 10),
            threshold: 20_000);

        ChatMessage[] reduced = [.. await reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken)];

        Assert.AreEqual(1, summarizer.CallCount);
        Assert.HasCount(3, reduced);
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

    [TestMethod]
    public async Task ReduceReactiveAsync_DoesNotPolluteAutomaticFailureBreaker()
    {
        OperationalContextChatReducer reducer = CreateReducer(
            new ThrowingSummarizer(),
            new FixedUsageProvider(300),
            threshold: 200);

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceReactiveAsync_Compacts_WhenUsageProviderReturnsNull()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new NullUsageProvider(),
            threshold: 20_000);

        ChatMessage[] reduced = [.. await reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken)];

        Assert.AreEqual(1, summarizer.CallCount);
        Assert.HasCount(3, reduced);
    }

    [TestMethod]
    public async Task ReduceAsync_SetsSummaryMetadata_ForCompactionArtifacts()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200);

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(
            [
                new ChatMessage(ChatRole.User, "user-1"),
                new ChatMessage(ChatRole.Assistant, "assistant-1"),
                new ChatMessage(ChatRole.User, "user-2"),
            ],
            TestContext.CancellationToken)];

        Assert.AreEqual(
            OperationalContextCompactionArtifactMetadata.CurrentSummaryFormatVersion,
            reduced[1].AdditionalProperties![OperationalContextCompactionArtifactMetadata.SummaryFormatVersionKey]);
        Assert.AreEqual(
            true,
            reduced[1].AdditionalProperties!.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.IsCompactionSummaryKey));
        Assert.AreEqual(
            true,
            reduced[1].AdditionalProperties!.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.HasPreservedTailKey));
    }

    [TestMethod]
    public async Task ReduceAsync_AppendsArtifacts_FromProviders_AndTracksCounts()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextChatReducer reducer = CreateReducer(
            summarizer,
            new FixedUsageProvider(300),
            threshold: 200,
            artifactProviders:
            [
                new RecordingArtifactProvider(
                    [
                        new ChatMessage(ChatRole.System, "attachment-1"),
                    ],
                    [
                        new ChatMessage(ChatRole.Assistant, "hook-1"),
                    ]),
            ]);

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(
            [
                new ChatMessage(ChatRole.System, "system-1"),
                new ChatMessage(ChatRole.User, "user-1"),
                new ChatMessage(ChatRole.Assistant, "assistant-1"),
                new ChatMessage(ChatRole.User, "user-2"),
            ],
            TestContext.CancellationToken)];

        Assert.AreEqual("Operational context boundary marker", reduced[1].Text);
        Assert.Contains("Operational summary checkpoint", reduced[2].Text);
        Assert.AreEqual("assistant-1", reduced[3].Text);
        Assert.AreEqual("user-2", reduced[4].Text);
        Assert.AreEqual("attachment-1", reduced[5].Text);
        Assert.AreEqual("hook-1", reduced[6].Text);
        Assert.AreEqual(
            1,
            reduced[2].AdditionalProperties![OperationalContextCompactionArtifactMetadata.AttachmentsCountKey]);
        Assert.AreEqual(
            1,
            reduced[2].AdditionalProperties![OperationalContextCompactionArtifactMetadata.HookResultsCountKey]);
    }

    private static OperationalContextChatReducer CreateReducer(
        IOperationalContextCompactionSummarizer summarizer,
        IOperationalContextCompactionUsageProvider usageProvider,
        int threshold,
        long contextWindowBufferTokens = 8_192,
        long summaryReservedOutputTokens = 4_096,
        IEnumerable<IOperationalContextCompactionArtifactProvider>? artifactProviders = null,
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
            artifactProviders,
            hooks,
            cleanupHandlers);

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

    private sealed class NullUsageProvider : IOperationalContextCompactionUsageProvider
    {
        public ValueTask<OperationalContextCompactionUsage?> GetUsageAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken) => ValueTask.FromResult<OperationalContextCompactionUsage?>(null);
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

    private sealed class RecordingArtifactProvider(
        IReadOnlyList<ChatMessage> attachmentMessages,
        IReadOnlyList<ChatMessage> hookResultMessages) : IOperationalContextCompactionArtifactProvider
    {
        public ValueTask<OperationalContextCompactionArtifacts> CreateArtifactsAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> messagesToKeep,
            OperationalContextCompactionReason reason,
            CancellationToken cancellationToken) => ValueTask.FromResult(new OperationalContextCompactionArtifacts
            {
                AttachmentMessages = attachmentMessages,
                HookResultMessages = hookResultMessages,
            });
    }
}
