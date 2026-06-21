using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

[TestClass]
public sealed class MessageEquivalenceComparerTests
{
    [TestMethod]
    public void AreEquivalent_ReturnsTrue_WhenAdditionalPropertiesMatchInDifferentOrder()
    {
        ChatMessage left = new(ChatRole.Assistant, "same")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["b"] = 2,
                ["a"] = "x",
            },
        };
        ChatMessage right = new(ChatRole.Assistant, "same")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["a"] = "x",
                ["b"] = 2,
            },
        };

        bool equivalent = MessageEquivalenceComparer.AreEquivalent([left], [right]);

        Assert.IsTrue(equivalent);
    }

    [TestMethod]
    public void AreEquivalent_ReturnsFalse_WhenContentsDiffer()
    {
        ChatMessage left = new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?> { ["value"] = 1 })]);
        ChatMessage right = new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?> { ["value"] = 2 })]);

        bool equivalent = MessageEquivalenceComparer.AreEquivalent([left], [right]);

        Assert.IsFalse(equivalent);
    }
}
