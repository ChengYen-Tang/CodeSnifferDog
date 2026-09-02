using CodeSnifferDog.Agents.Common.TokenUsage;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Agents.Common.TokenUsage;

[TestClass]
public sealed class TokenUsageLedgerTests
{
    [TestMethod]
    public void CreatePrediction_AfterProviderCheckpoint_EstimatesOnlyTheAppendedMessages()
    {
        TokenUsageLedger ledger = new();
        ChatMessage firstMessage = new(ChatRole.User, new string('a', 400));
        IReadOnlyList<ChatMessage> checkpointMessages = [firstMessage];
        const string requestFingerprint = "request-a";
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(
            checkpointMessages,
            "model-a",
            requestFingerprint);

        _ = ledger.RecordSuccessfulResponse(
            checkpointMessages,
            firstPrediction,
            actualInputTokens: 1_000,
            modelId: "model-a",
            requestFingerprint: requestFingerprint);

        ChatMessage appendedToolResult = new(ChatRole.Tool, [
            new FunctionResultContent("call-1", new { Output = new string('b', 200) }),
        ]);
        IReadOnlyList<ChatMessage> nextMessages = [firstMessage, appendedToolResult];

        TokenUsagePrediction prediction = ledger.CreatePrediction(
            nextMessages,
            "model-a",
            requestFingerprint);

        Assert.IsTrue(prediction.UsesProviderCheckpoint);
        Assert.AreEqual(1_000, prediction.CheckpointInputTokens);
        Assert.AreEqual(TokenEstimator.Estimate(nextMessages), prediction.RawEstimateTokens);
        Assert.AreEqual(TokenEstimator.Estimate([appendedToolResult]), prediction.DeltaEstimateTokens);
        Assert.AreEqual(1_000 + prediction.DeltaEstimateTokens, prediction.CalibratedEstimateTokens);
    }

    [TestMethod]
    public void CreatePrediction_WhenProviderCheckpointIsLowerThanLocalEstimate_UsesANegativeInputTokenAdjustment()
    {
        TokenUsageLedger ledger = new();
        ChatMessage message = new(ChatRole.User, new string('x', 400));
        IReadOnlyList<ChatMessage> messages = [message];
        const string requestFingerprint = "request-a";
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(messages, "model-a", requestFingerprint);
        Assert.IsGreaterThan(1, firstPrediction.RawEstimateTokens);

        int expectedInputTokens = firstPrediction.RawEstimateTokens - 1;
        _ = ledger.RecordSuccessfulResponse(
            messages,
            firstPrediction,
            expectedInputTokens,
            "model-a",
            requestFingerprint);

        TokenUsagePrediction prediction = ledger.CreatePrediction(messages, "model-a", requestFingerprint);

        Assert.IsTrue(prediction.UsesProviderCheckpoint);
        Assert.AreEqual(expectedInputTokens, prediction.CalibratedEstimateTokens);
        Assert.AreEqual(-1, prediction.InputTokenAdjustmentTokens);
    }

    [TestMethod]
    public void CreatePrediction_WhenModelChanges_DoesNotReuseThePreviousCheckpoint()
    {
        TokenUsageLedger ledger = new();
        ChatMessage message = new(ChatRole.User, "input");
        IReadOnlyList<ChatMessage> messages = [message];
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(messages, "model-a");

        _ = ledger.RecordSuccessfulResponse(messages, firstPrediction, actualInputTokens: 1_000, modelId: "model-a");

        TokenUsagePrediction prediction = ledger.CreatePrediction(messages, "model-b");

        Assert.IsFalse(prediction.UsesProviderCheckpoint);
        Assert.IsNull(prediction.CheckpointInputTokens);
    }

    [TestMethod]
    public void CreatePrediction_WhenRequestFingerprintChanges_DoesNotReuseThePreviousCheckpoint()
    {
        TokenUsageLedger ledger = new();
        ChatMessage message = new(ChatRole.User, "input");
        IReadOnlyList<ChatMessage> messages = [message];
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(messages, "model-a", "request-a");

        _ = ledger.RecordSuccessfulResponse(
            messages,
            firstPrediction,
            actualInputTokens: 1_000,
            modelId: "model-a",
            requestFingerprint: "request-a");

        TokenUsagePrediction prediction = ledger.CreatePrediction(messages, "model-a", "request-b");

        Assert.IsFalse(prediction.UsesProviderCheckpoint);
        Assert.IsNull(prediction.CheckpointInputTokens);
    }

    [TestMethod]
    public void CreatePrediction_WhenModelIdentityIsUnknown_DoesNotReuseThePreviousCheckpoint()
    {
        TokenUsageLedger ledger = new();
        ChatMessage message = new(ChatRole.User, "input");
        IReadOnlyList<ChatMessage> messages = [message];
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(messages, "model-a", "request-a");

        _ = ledger.RecordSuccessfulResponse(
            messages,
            firstPrediction,
            actualInputTokens: 1_000,
            modelId: "model-a",
            requestFingerprint: "request-a");

        TokenUsagePrediction prediction = ledger.CreatePrediction(messages, null, "request-a");

        Assert.IsFalse(prediction.UsesProviderCheckpoint);
        Assert.IsNull(prediction.CheckpointInputTokens);
    }

    [TestMethod]
    public void CreatePrediction_WhenRequestIdentityIsUnknown_DoesNotReuseThePreviousCheckpoint()
    {
        TokenUsageLedger ledger = new();
        ChatMessage message = new(ChatRole.User, "input");
        IReadOnlyList<ChatMessage> messages = [message];
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(messages, "model-a", "request-a");

        _ = ledger.RecordSuccessfulResponse(
            messages,
            firstPrediction,
            actualInputTokens: 1_000,
            modelId: "model-a",
            requestFingerprint: "request-a");

        TokenUsagePrediction prediction = ledger.CreatePrediction(messages, "model-a", null);

        Assert.IsFalse(prediction.UsesProviderCheckpoint);
        Assert.IsNull(prediction.CheckpointInputTokens);
    }

    [TestMethod]
    public void RecordContextWindowExceeded_RequiresRecoveryUntilAProviderRequestSucceeds()
    {
        TokenUsageLedger ledger = new();
        ChatMessage message = new(ChatRole.User, "input");
        IReadOnlyList<ChatMessage> messages = [message];

        ledger.RecordContextWindowExceeded();

        Assert.IsTrue(ledger.CreatePrediction(messages).RequiresCompactionRecovery);

        TokenUsagePrediction prediction = ledger.CreatePrediction(messages);
        _ = ledger.RecordSuccessfulResponse(messages, prediction, actualInputTokens: null);

        Assert.IsFalse(ledger.CreatePrediction(messages).RequiresCompactionRecovery);
    }

    [TestMethod]
    public void CreatePrediction_UsesProviderOutputTokensForReplayableAssistantSuffix()
    {
        TokenUsageLedger ledger = new();
        ChatMessage requestMessage = new(ChatRole.User, "input");
        IReadOnlyList<ChatMessage> requestMessages = [requestMessage];
        TokenUsagePrediction firstPrediction = ledger.CreatePrediction(requestMessages, "model-a", "request-a");
        ChatMessage assistantResponse = new(ChatRole.Assistant, new string('x', 4_000));

        _ = ledger.RecordSuccessfulResponse(
            requestMessages,
            firstPrediction,
            actualInputTokens: 1_000,
            modelId: "model-a",
            requestFingerprint: "request-a",
            actualOutputTokens: 500,
            replayableOutputTokens: 500,
            responseMessages: [assistantResponse]);

        TokenUsagePrediction nextPrediction = ledger.CreatePrediction(
            [requestMessage, assistantResponse],
            "model-a",
            "request-a");

        Assert.IsTrue(nextPrediction.UsesProviderCheckpoint);
        Assert.AreEqual(500, nextPrediction.DeltaEstimateTokens);
        Assert.AreEqual(500, nextPrediction.ReplayableOutputTokens);
        Assert.IsNotNull(nextPrediction.EstimatedReplayableOutputTokens);
    }

    [TestMethod]
    public void GetReplayableOutputTokenCount_DoesNotTreatUnclassifiedOutputAsPromptTokens()
    {
        ChatMessage assistantResponse = new(ChatRole.Assistant, [new FunctionCallContent(
            "call-1",
            "TestTool",
            new Dictionary<string, object?>())]);

        int? replayableTokens = TokenUsageLedger.GetReplayableOutputTokenCount(
            new UsageDetails { OutputTokenCount = 500 },
            [assistantResponse]);

        Assert.IsNull(replayableTokens);
    }

    [TestMethod]
    public void GetReplayableOutputTokenCount_UsesProviderReasoningBreakdown()
    {
        int? replayableTokens = TokenUsageLedger.GetReplayableOutputTokenCount(
            new UsageDetails
            {
                OutputTokenCount = 500,
                ReasoningTokenCount = 125,
            },
            [new ChatMessage(ChatRole.Assistant, "visible response")]);

        Assert.AreEqual(375, replayableTokens);
    }
}
