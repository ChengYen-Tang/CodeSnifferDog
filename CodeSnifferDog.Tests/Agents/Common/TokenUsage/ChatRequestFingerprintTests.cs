using CodeSnifferDog.Agents.Common.TokenUsage;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Agents.Common.TokenUsage;

[TestClass]
public sealed class ChatRequestFingerprintTests
{
    [TestMethod]
    public void Cache_ReusesFingerprintForAnUnchangedContract()
    {
        ChatRequestFingerprintCache cache = new();
        ChatOptions options = new()
        {
            Instructions = "instructions",
            ModelId = "model-a",
        };

        string? first = cache.Get(options);
        string? second = cache.Get(options);

        Assert.IsNotNull(first);
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void Cache_RecomputesWhenAnOptionChanges()
    {
        ChatRequestFingerprintCache cache = new();
        ChatOptions options = new()
        {
            Instructions = "instructions",
            ModelId = "model-a",
        };

        string? first = cache.Get(options);
        options.Temperature = 0.5f;
        string? second = cache.Get(options);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void Cache_RecomputesWhenToolContractIsReplaced()
    {
        ChatRequestFingerprintCache cache = new();
        AITool firstTool = AIFunctionFactory.Create(
            (string value) => value,
            "Echo",
            "Echoes a string.");
        AITool secondTool = AIFunctionFactory.Create(
            (int value) => value,
            "Echo",
            "Echoes an integer.");
        ChatOptions options = new()
        {
            Tools = [firstTool],
        };

        string? first = cache.Get(options);
        options.Tools = [secondTool];
        string? second = cache.Get(options);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void Create_WithEquivalentOptions_ReturnsTheSameFingerprint()
    {
        string? first = ChatRequestFingerprint.Create(new ChatOptions
        {
            Instructions = "instructions",
            ModelId = "model-a",
            StopSequences = ["stop"],
        });
        string? second = ChatRequestFingerprint.Create(new ChatOptions
        {
            Instructions = "instructions",
            ModelId = "model-a",
            StopSequences = ["stop"],
        });

        Assert.IsNotNull(first);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Create_IncludesToolSchemaAndImportantChatOptions()
    {
        AITool stringTool = AIFunctionFactory.Create(
            (string value) => value,
            "Echo",
            "Echoes a string.");
        AITool integerTool = AIFunctionFactory.Create(
            (int value) => value,
            "Echo",
            "Echoes a string.");

        string? baseline = ChatRequestFingerprint.Create(new ChatOptions
        {
            Tools = [stringTool],
            ResponseFormat = ChatResponseFormat.Text,
            Temperature = 0.1f,
        });
        string? changedSchema = ChatRequestFingerprint.Create(new ChatOptions
        {
            Tools = [integerTool],
            ResponseFormat = ChatResponseFormat.Text,
            Temperature = 0.1f,
        });
        string? changedOptions = ChatRequestFingerprint.Create(new ChatOptions
        {
            Tools = [stringTool],
            ResponseFormat = ChatResponseFormat.Json,
            Temperature = 0.2f,
        });

        Assert.IsNotNull(baseline);
        Assert.AreNotEqual(baseline, changedSchema);
        Assert.AreNotEqual(baseline, changedOptions);
    }

    [TestMethod]
    public void Create_WhenIdentityCannotBeConfirmed_ReturnsNull()
    {
        Assert.IsNull(ChatRequestFingerprint.Create(null));
        Assert.IsNull(ChatRequestFingerprint.Create(new ChatOptions
        {
            RawRepresentationFactory = static _ => new object(),
        }));
    }
}
