using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;

[TestClass]
public sealed class FrameworkCompactionPlannerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task PlanAsync_PreservesWholeToolCallGroup_WhenTailStartsInsideTheGroup()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result")]),
            new(ChatRole.Assistant, "final response"),
        ];

        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 2));
        CompactionPlan plan = await planner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.IsTrue(plan.ShouldCompact);
        Assert.HasCount(3, plan.MessagesToKeep);
        Assert.AreSame(messages[1], plan.MessagesToKeep[0]);
        Assert.AreSame(messages[2], plan.MessagesToKeep[1]);
        Assert.AreSame(messages[3], plan.MessagesToKeep[2]);
    }

    [TestMethod]
    public async Task PlanAsync_UsesGroupTokenEstimate_ForAtomicToolTail()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "old"),
            new(ChatRole.Assistant, [new FunctionCallContent("c", "T", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("c", "r")]),
            new(ChatRole.Assistant, "f"),
        ];
        int groupTokenEstimate = TokenEstimator.Estimate(messages[1..3]);
        int perMessageTokenEstimate =
            TokenEstimator.Estimate([messages[1]]) + TokenEstimator.Estimate([messages[2]]);
        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 1, minimumTokens: 3));

        CompactionPlan plan = await planner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        // The atomic tool group is estimated once, so its two short messages consume one token together.
        Assert.AreEqual(1, groupTokenEstimate);
        Assert.AreEqual(2, perMessageTokenEstimate);
        Assert.IsTrue(plan.ShouldCompact);
        Assert.HasCount(4, plan.MessagesToKeep);
        Assert.AreSame(messages[0], plan.MessagesToKeep[0]);
    }

    [TestMethod]
    public async Task PlanAsync_PreservesAllParallelToolResultsAsOneFrameworkGroup()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant,
            [
                new FunctionCallContent("call-a", "ToolA", new Dictionary<string, object?>()),
                new FunctionCallContent("call-b", "ToolB", new Dictionary<string, object?>()),
            ]),
            new(ChatRole.Tool, [new FunctionResultContent("call-a", "result-a")]),
            new(ChatRole.Tool, [new FunctionResultContent("call-b", "result-b")]),
            new(ChatRole.Assistant, "final response"),
        ];

        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 2));
        CompactionPlan plan = await planner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.HasCount(4, plan.MessagesToKeep);
        CollectionAssert.AreEqual(
            new[] { "call-a", "call-b" },
            plan.MessagesToKeep
                .SelectMany(static message => message.Contents.OfType<FunctionCallContent>())
                .Select(static call => call.CallId)
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { "call-a", "call-b" },
            plan.MessagesToKeep
                .SelectMany(static message => message.Contents.OfType<FunctionResultContent>())
                .Select(static result => result.CallId)
                .ToArray());
    }

    [TestMethod]
    public async Task PlanAsync_PreservesReasoningWithItsToolCallGroup()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "older request"),
            new(ChatRole.Assistant, [new TextReasoningContent("Need to inspect the target first.")]),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result")]),
            new(ChatRole.Assistant, "final response"),
        ];

        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 2));
        CompactionPlan plan = await planner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.HasCount(4, plan.MessagesToKeep);
        Assert.AreSame(messages[1], plan.MessagesToKeep[0]);
        Assert.AreSame(messages[2], plan.MessagesToKeep[1]);
        Assert.AreSame(messages[3], plan.MessagesToKeep[2]);
        Assert.AreSame(messages[4], plan.MessagesToKeep[3]);
    }

    [TestMethod]
    public async Task PlanAsync_MatchesLegacyTailPolicy_ForRepresentativeTranscript()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request-1"),
            new(ChatRole.Assistant, "response-1"),
            new(ChatRole.User, "request-2"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result-1")]),
            new(ChatRole.Assistant, "response-2"),
            new(ChatRole.User, "request-3"),
            new(ChatRole.Assistant, "response-3"),
        ];
        CompactionOptions options = CreateOptions(minimumMessages: 3);

        FrameworkCompactionPlanner frameworkPlanner = new(options);
        LegacyCompactionPlanner legacyPlanner = new(options);
        CompactionPlan frameworkPlan = await frameworkPlanner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);
        CompactionPlan legacyPlan = await legacyPlanner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.AreEqual(legacyPlan.ShouldCompact, frameworkPlan.ShouldCompact);
        CollectionAssert.AreEqual(legacyPlan.MessagesToKeep.ToArray(), frameworkPlan.MessagesToKeep.ToArray());
    }

    [TestMethod]
    public async Task PlanAsync_DoesNotTriggerAutomaticCompaction_BelowTheThreshold()
    {
        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 1));

        CompactionPlan plan = await planner.PlanAsync(
            [
                new ChatMessage(ChatRole.User, "small request"),
                new ChatMessage(ChatRole.Assistant, "small response"),
            ],
            CompactionReason.AutomaticThreshold,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.IsFalse(plan.ShouldCompact);
        Assert.IsEmpty(plan.MessagesToKeep);
    }

    [TestMethod]
    public async Task PlanAsync_TriggersAutomaticCompaction_WhenPositiveInputTokenAdjustmentReachesThreshold()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, "response"),
        ];
        int transcriptTokens = TokenEstimator.Estimate(messages);
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = transcriptTokens + 3,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 1,
            PreservedTailMaxTokens = 100_000,
        };
        FrameworkCompactionPlanner planner = new(options);

        CompactionPlan withoutBias = await planner.PlanAsync(
            messages,
            CompactionReason.AutomaticThreshold,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);
        CompactionPlan withBias = await planner.PlanAsync(
            messages,
            CompactionReason.AutomaticThreshold,
            inputTokenAdjustmentTokens: 1,
            TestContext.CancellationToken);

        Assert.IsFalse(withoutBias.ShouldCompact);
        Assert.IsTrue(withBias.ShouldCompact);
    }

    [TestMethod]
    public async Task PlanAsync_DoesNotTriggerAutomaticCompaction_WhenNegativeInputTokenAdjustmentDropsBelowThreshold()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, "response"),
        ];
        int transcriptTokens = TokenEstimator.Estimate(messages);
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = transcriptTokens + 2,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 1,
            PreservedTailMaxTokens = 100_000,
        };
        FrameworkCompactionPlanner planner = new(options);

        CompactionPlan plan = await planner.PlanAsync(
            messages,
            CompactionReason.AutomaticThreshold,
            inputTokenAdjustmentTokens: -1,
            TestContext.CancellationToken);

        Assert.IsFalse(plan.ShouldCompact);
    }

    [TestMethod]
    public async Task PlanAsync_UsesJsonAwareStructuredPayloadEstimate_ForAutomaticThreshold()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool,
            [
                new FunctionResultContent(
                    "call-1",
                    new { Payload = new string('x', 1_000) }),
            ]),
        ];
        int transcriptTokens = TokenEstimator.Estimate(messages);
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = transcriptTokens + 2,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 1,
            PreservedTailMaxTokens = 100_000,
        };
        FrameworkCompactionPlanner planner = new(options);

        CompactionPlan plan = await planner.PlanAsync(
            messages,
            CompactionReason.AutomaticThreshold,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.IsTrue(plan.ShouldCompact);
    }

    [TestMethod]
    public async Task PlanAsync_UsesLegacyPlanner_WhenFrameworkHasOnlyOneNonSystemGroup()
    {
        ChatMessage[] messages = [new(ChatRole.User, "only request")];
        CompactionOptions options = CreateOptions(minimumMessages: 1);
        FrameworkCompactionPlanner frameworkPlanner = new(options);
        LegacyCompactionPlanner legacyPlanner = new(options);

        CompactionPlan frameworkPlan = await frameworkPlanner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);
        CompactionPlan legacyPlan = await legacyPlanner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.AreEqual(legacyPlan.ShouldCompact, frameworkPlan.ShouldCompact);
        CollectionAssert.AreEqual(legacyPlan.MessagesToKeep.ToArray(), frameworkPlan.MessagesToKeep.ToArray());
    }

    [TestMethod]
    public async Task PlanAsync_ReactiveCompactionBypassesAutomaticThreshold()
    {
        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 1));

        CompactionPlan plan = await planner.PlanAsync(
            [
                new ChatMessage(ChatRole.User, "small request"),
                new ChatMessage(ChatRole.Assistant, "small response"),
            ],
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.IsTrue(plan.ShouldCompact);
    }

    [TestMethod]
    public async Task PlanAsync_ForwardsLoggerFactoryToFrameworkCompactionStrategy()
    {
        RecordingLoggerFactory loggerFactory = new();
        FrameworkCompactionPlanner planner = new(CreateOptions(minimumMessages: 1), loggerFactory);
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, "response"),
        ];

        _ = await planner.PlanAsync(
            messages,
            CompactionReason.Reactive,
            inputTokenAdjustmentTokens: 0,
            TestContext.CancellationToken);

        Assert.IsNotEmpty(loggerFactory.Entries);
        Assert.IsTrue(
            loggerFactory.Entries.Any(static entry =>
                entry.Message.Contains("Compaction completed", StringComparison.Ordinal)),
            "The Framework compaction strategy should emit diagnostics through the supplied logger factory.");
    }

    private static CompactionOptions CreateOptions(int minimumMessages, int minimumTokens = 1) => new()
    {
        ModelContextWindowTokens = 100_000,
        PreservedTailMinTokens = minimumTokens,
        PreservedTailMinMessages = minimumMessages,
        PreservedTailMaxTokens = 100_000,
    };

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(
        string categoryName,
        List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(categoryName, logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);
}
