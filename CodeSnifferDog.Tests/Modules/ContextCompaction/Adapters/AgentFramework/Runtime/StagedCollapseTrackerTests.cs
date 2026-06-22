using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

[TestClass]
public sealed class StagedCollapseTrackerTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CommitNew_CommitsOnlyCollapsesStagedAfterTrackerCreation()
    {
        (OperationalContextAgentCompactionOptions options, OperationalContextCollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("initial");
        await StageCollapseAsync(options, sessionState, session, messages);
        StagedCollapseTracker tracker = new(session, options);

        await StageCollapseAsync(options, sessionState, session, CreateMessages("new"));
        tracker.CommitNew();

        OperationalContextCollapseState state = sessionState.Get(session);
        Assert.HasCount(1, state.Commits);
        Assert.HasCount(1, state.StagedSpans);
        Assert.AreEqual("0000000000000002", state.Commits[0].CollapseId);
        Assert.AreEqual("0000000000000001", state.StagedSpans[0].CollapseId);
    }

    [TestMethod]
    public async Task DiscardNew_DiscardsOnlyCollapsesStagedAfterTrackerCreation()
    {
        (OperationalContextAgentCompactionOptions options, OperationalContextCollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        await StageCollapseAsync(options, sessionState, session, CreateMessages("initial"));
        StagedCollapseTracker tracker = new(session, options);

        await StageCollapseAsync(options, sessionState, session, CreateMessages("new"));
        tracker.DiscardNew();

        OperationalContextCollapseState state = sessionState.Get(session);
        Assert.IsEmpty(state.Commits);
        Assert.HasCount(1, state.StagedSpans);
        Assert.AreEqual("0000000000000001", state.StagedSpans[0].CollapseId);
    }

    [TestMethod]
    public async Task CommitAndPrepareRetryMessages_ReturnsCommittedProjection()
    {
        (OperationalContextAgentCompactionOptions options, OperationalContextCollapseSessionState sessionState) = CreateOptions();
        TestSession session = new();
        ChatMessage[] messages = CreateMessages("retry");
        StagedCollapseTracker tracker = new(session, options);
        await StageCollapseAsync(options, sessionState, session, messages);

        ChatMessage[] retryMessages = [.. tracker.CommitAndPrepareRetryMessages(messages, tracker.CaptureNewIds())];

        Assert.IsTrue(retryMessages.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.CollapseProjectionArtifactKind));
        Assert.IsFalse(retryMessages.SequenceEqual(messages));
        OperationalContextCollapseState state = sessionState.Get(session);
        Assert.HasCount(1, state.Commits);
        Assert.IsEmpty(state.StagedSpans);
    }

    private async Task StageCollapseAsync(
        OperationalContextAgentCompactionOptions options,
        OperationalContextCollapseSessionState sessionState,
        AgentSession session,
        IReadOnlyList<ChatMessage> messages)
    {
        OperationalContextCompactionResult result = await options.Reducer
            .CompactReactiveAsync(messages, TestContext.CancellationToken)
            .ConfigureAwait(false);
        sessionState.StageCollapseSpan(session, result, OperationalContextCompactionReason.ContextCollapseProactive);
    }

    private static ChatMessage[] CreateMessages(string suffix) =>
    [
        new(ChatRole.User, $"user-{suffix}"),
        new(ChatRole.Assistant, $"assistant-{suffix}"),
        new(ChatRole.User, new string('x', 1_000)),
    ];

    private static (OperationalContextAgentCompactionOptions Options, OperationalContextCollapseSessionState SessionState) CreateOptions()
    {
        OperationalContextCollapseSessionState sessionState = new();
        OperationalContextChatReducer reducer = new(
            new OperationalContextCompactionOptions
            {
                ModelContextWindowTokens = 100_000,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                CollapseProactiveThresholdPercentage = 1,
                CollapseBlockingThresholdPercentage = 100,
                Mode = OperationalContextCompactionMode.ContextCollapse,
            },
            new StaticOperationalContextSummaryPromptProvider("summarize"),
            new RecordingSummarizer());

        return (new OperationalContextAgentCompactionOptions
        {
            Reducer = reducer,
            CollapseController = new OperationalContextCollapseController(reducer, sessionState: sessionState),
        }, sessionState);
    }

    private sealed class RecordingSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult("<summary>Current objective\nCompleted work\nNext steps</summary>");
    }

    private sealed class TestSession : AgentSession;
}
