using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Shrinking;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core;

[TestClass]
public sealed class MessageShrinkerTests
{
    private static readonly string[] Call3Only =
    [
        "call-3",
    ];

    [TestMethod]
    public void ApplyMicroCompaction_RewritesOlderCompactableToolResults_AsStructuredArtifacts()
    {
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "RunShellCommand"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "RunShellCommand"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "RunShellCommand"),
            CreateToolResult("call-3", "result-3"),
        ];

        MessageShrinkResult result = MessageShrinker.ApplyMicroCompaction(
            messages,
            new CompactionOptions
            {
                ModelContextWindowTokens = 100,
                MicroCompactTriggerToolResultCount = 3,
                MicroCompactKeepRecentToolResultCount = 1,
            });

        FunctionResultContent[] toolResults = [.. result.Messages
            .SelectMany(static message => message.Contents.OfType<FunctionResultContent>())];

        Assert.HasCount(3, toolResults);
        string? firstResult = toolResults[0].Result?.ToString();
        string? secondResult = toolResults[1].Result?.ToString();
        Assert.IsNotNull(firstResult);
        Assert.IsNotNull(secondResult);
        Assert.Contains("[Compacted tool result]", firstResult);
        Assert.Contains("Tool: RunShellCommand", firstResult);
        Assert.Contains("CallId: call-1", firstResult);
        Assert.Contains("CallId: call-2", secondResult);
        Assert.AreEqual("result-3", toolResults[2].Result?.ToString());
    }

    [TestMethod]
    public void ApplySnip_RemovesOlderCompactableToolCallsAndResults_AndAddsBoundary()
    {
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "RunShellCommand"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "RunShellCommand"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "RunShellCommand"),
            CreateToolResult("call-3", "result-3"),
        ];

        MessageShrinkResult result = MessageShrinker.ApplySnip(
            messages,
            new CompactionOptions
            {
                ModelContextWindowTokens = 100,
                SnipTriggerToolResultCount = 3,
                SnipKeepRecentToolResultCount = 1,
            });

        Assert.AreEqual("Operational snip boundary", result.Messages[0].Text);
        Assert.AreEqual(
            CompactionArtifactMetadata.SnipBoundaryArtifactKind,
            result.Messages[0].AdditionalProperties?.GetValueOrDefault(
                CompactionArtifactMetadata.ArtifactKindKey)?.ToString());

        string[] remainingCallIds = [.. result.Messages
            .SelectMany(static message => message.Contents.OfType<FunctionCallContent>())
            .Select(static call => call.CallId)];

        string[] remainingResultCallIds = [.. result.Messages
            .SelectMany(static message => message.Contents.OfType<FunctionResultContent>())
            .Select(static result => result.CallId)];

        CollectionAssert.AreEqual(Call3Only, remainingCallIds);
        CollectionAssert.AreEqual(Call3Only, remainingResultCallIds);
    }

    [TestMethod]
    public void ApplyMicroCompaction_DoesNotTouchNonCompactableTools()
    {
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "CreateRuleReviewIssue"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "CreateRuleReviewIssue"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "CreateRuleReviewIssue"),
            CreateToolResult("call-3", "result-3"),
        ];

        MessageShrinkResult result = MessageShrinker.ApplyMicroCompaction(
            messages,
            new CompactionOptions
            {
                ModelContextWindowTokens = 100,
                MicroCompactTriggerToolResultCount = 3,
                MicroCompactKeepRecentToolResultCount = 1,
            });

        Assert.IsFalse(result.WasChanged);
        CollectionAssert.AreEqual(messages, result.Messages.ToArray());
    }

    [TestMethod]
    public void ApplyMicroCompaction_AddsShrinkMetadataToRewrittenMessages()
    {
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "RunShellCommand"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "RunShellCommand"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "RunShellCommand"),
            CreateToolResult("call-3", "result-3"),
        ];

        MessageShrinkResult result = MessageShrinker.ApplyMicroCompaction(
            messages,
            new CompactionOptions
            {
                ModelContextWindowTokens = 100,
                MicroCompactTriggerToolResultCount = 3,
                MicroCompactKeepRecentToolResultCount = 1,
            });

        ChatMessage rewrittenToolMessage = result.Messages[1];

        Assert.IsNotNull(rewrittenToolMessage.AdditionalProperties);
        Assert.AreEqual(
            "microcompact",
            rewrittenToolMessage.AdditionalProperties[CompactionArtifactMetadata.ShrinkOperationKey]?.ToString());
    }

    private static ChatMessage CreateToolCall(string callId, string toolName) =>
        new(ChatRole.Assistant, [new FunctionCallContent(callId, toolName, new Dictionary<string, object?>())]);

    private static ChatMessage CreateToolResult(string callId, string result) =>
        new(ChatRole.Tool, [new FunctionResultContent(callId, result)]);
}
