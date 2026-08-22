using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;

[TestClass]
public sealed class FrameworkCompactionTailSelectorTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task SelectAsync_PreservesWholeToolCallGroup_WhenTailStartsInsideTheGroup()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result")]),
            new(ChatRole.Assistant, "final response"),
        ];

        FrameworkCompactionTailSelector selector = new();
        IReadOnlyList<ChatMessage> selected = await selector.SelectAsync(
            messages,
            CreateOptions(minimumMessages: 2),
            TestContext.CancellationToken);

        Assert.HasCount(3, selected);
        Assert.AreSame(messages[1], selected[0]);
        Assert.AreSame(messages[2], selected[1]);
        Assert.AreSame(messages[3], selected[2]);
    }

    [TestMethod]
    public async Task SelectAsync_PreservesAllParallelToolResultsAsOneFrameworkGroup()
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

        FrameworkCompactionTailSelector selector = new();
        IReadOnlyList<ChatMessage> selected = await selector.SelectAsync(
            messages,
            CreateOptions(minimumMessages: 2),
            TestContext.CancellationToken);

        Assert.HasCount(4, selected);
        CollectionAssert.AreEqual(
            new[] { "call-a", "call-b" },
            selected
                .SelectMany(static message => message.Contents.OfType<FunctionCallContent>())
                .Select(static call => call.CallId)
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { "call-a", "call-b" },
            selected
                .SelectMany(static message => message.Contents.OfType<FunctionResultContent>())
                .Select(static result => result.CallId)
                .ToArray());
    }

    [TestMethod]
    public async Task SelectAsync_MatchesLegacyTailPolicy_ForRepresentativeTranscript()
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

        FrameworkCompactionTailSelector frameworkSelector = new();
        LegacyCompactionTailSelector legacySelector = new();
        IReadOnlyList<ChatMessage> frameworkSelected = await frameworkSelector.SelectAsync(
            messages,
            options,
            TestContext.CancellationToken);
        IReadOnlyList<ChatMessage> legacySelected = await legacySelector.SelectAsync(
            messages,
            options,
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(legacySelected.ToArray(), frameworkSelected.ToArray());
    }

    [TestMethod]
    public async Task SelectAsync_ForwardsLoggerFactoryToFrameworkCompactionStrategy()
    {
        RecordingLoggerFactory loggerFactory = new();
        FrameworkCompactionTailSelector selector = new(loggerFactory);
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, "response"),
        ];

        _ = await selector.SelectAsync(
            messages,
            CreateOptions(minimumMessages: 1),
            TestContext.CancellationToken);

        Assert.IsNotEmpty(loggerFactory.Entries);
        Assert.IsTrue(
            loggerFactory.Entries.Any(static entry =>
                entry.Message.Contains("Compaction completed", StringComparison.Ordinal)),
            "The Framework compaction strategy should emit diagnostics through the supplied logger factory.");
    }

    private static CompactionOptions CreateOptions(int minimumMessages) => new()
    {
        ModelContextWindowTokens = 100_000,
        PreservedTailMinTokens = 1,
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
