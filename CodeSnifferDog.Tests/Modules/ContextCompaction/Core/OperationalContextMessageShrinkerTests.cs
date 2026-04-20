using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core;

[TestClass]
public sealed class OperationalContextMessageShrinkerTests
{
    [TestMethod]
    public void ApplyMicroCompaction_RewritesOlderCompactableToolResults_AsStructuredArtifacts()
    {
        OperationalContextMessageShrinker shrinker = new();
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "RunShellCommand"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "RunShellCommand"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "RunShellCommand"),
            CreateToolResult("call-3", "result-3"),
        ];

        OperationalContextMessageShrinkResult result = shrinker.ApplyMicroCompaction(
            messages,
            new OperationalContextCompactionOptions
            {
                ModelContextWindowTokens = 100,
                MicroCompactTriggerToolResultCount = 3,
                MicroCompactKeepRecentToolResultCount = 1,
            });

        FunctionResultContent[] toolResults = [.. result.Messages
            .SelectMany(static message => message.Contents.OfType<FunctionResultContent>())];

        Assert.HasCount(3, toolResults);
        StringAssert.Contains(toolResults[0].Result?.ToString(), "[Compacted tool result]");
        StringAssert.Contains(toolResults[0].Result?.ToString(), "Tool: RunShellCommand");
        StringAssert.Contains(toolResults[0].Result?.ToString(), "CallId: call-1");
        StringAssert.Contains(toolResults[1].Result?.ToString(), "CallId: call-2");
        Assert.AreEqual("result-3", toolResults[2].Result?.ToString());
    }

    [TestMethod]
    public void ApplySnip_RemovesOlderCompactableToolCallsAndResults_AndAddsBoundary()
    {
        OperationalContextMessageShrinker shrinker = new();
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "RunShellCommand"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "RunShellCommand"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "RunShellCommand"),
            CreateToolResult("call-3", "result-3"),
        ];

        OperationalContextMessageShrinkResult result = shrinker.ApplySnip(
            messages,
            new OperationalContextCompactionOptions
            {
                ModelContextWindowTokens = 100,
                SnipTriggerToolResultCount = 3,
                SnipKeepRecentToolResultCount = 1,
            });

        Assert.AreEqual("Operational snip boundary", result.Messages[0].Text);
        Assert.AreEqual(
            OperationalContextCompactionArtifactMetadata.SnipBoundaryArtifactKind,
            result.Messages[0].AdditionalProperties?.GetValueOrDefault(
                OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString());

        string[] remainingCallIds = [.. result.Messages
            .SelectMany(static message => message.Contents.OfType<FunctionCallContent>())
            .Select(static call => call.CallId)];

        string[] remainingResultCallIds = [.. result.Messages
            .SelectMany(static message => message.Contents.OfType<FunctionResultContent>())
            .Select(static result => result.CallId)];

        CollectionAssert.AreEqual(new[] { "call-3" }, remainingCallIds);
        CollectionAssert.AreEqual(new[] { "call-3" }, remainingResultCallIds);
    }

    [TestMethod]
    public void ApplyMicroCompaction_DoesNotTouchNonCompactableTools()
    {
        OperationalContextMessageShrinker shrinker = new();
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "CreateRuleReviewIssue"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "CreateRuleReviewIssue"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "CreateRuleReviewIssue"),
            CreateToolResult("call-3", "result-3"),
        ];

        OperationalContextMessageShrinkResult result = shrinker.ApplyMicroCompaction(
            messages,
            new OperationalContextCompactionOptions
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
        OperationalContextMessageShrinker shrinker = new();
        ChatMessage[] messages =
        [
            CreateToolCall("call-1", "RunShellCommand"),
            CreateToolResult("call-1", "result-1"),
            CreateToolCall("call-2", "RunShellCommand"),
            CreateToolResult("call-2", "result-2"),
            CreateToolCall("call-3", "RunShellCommand"),
            CreateToolResult("call-3", "result-3"),
        ];

        OperationalContextMessageShrinkResult result = shrinker.ApplyMicroCompaction(
            messages,
            new OperationalContextCompactionOptions
            {
                ModelContextWindowTokens = 100,
                MicroCompactTriggerToolResultCount = 3,
                MicroCompactKeepRecentToolResultCount = 1,
            });

        ChatMessage rewrittenToolMessage = result.Messages[1];

        Assert.IsNotNull(rewrittenToolMessage.AdditionalProperties);
        Assert.AreEqual(
            "microcompact",
            rewrittenToolMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.ShrinkOperationKey]?.ToString());
    }

    private static ChatMessage CreateToolCall(string callId, string toolName) =>
        new(ChatRole.Assistant, [new FunctionCallContent(callId, toolName, new Dictionary<string, object?>())]);

    private static ChatMessage CreateToolResult(string callId, string result) =>
        new(ChatRole.Tool, [new FunctionResultContent(callId, result)]);
}
