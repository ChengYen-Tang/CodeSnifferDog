using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core.Providers;

[TestClass]
public sealed class EstimatingOperationalContextCompactionUsageProviderTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetUsageAsync_IncludesMessageContents()
    {
        EstimatingOperationalContextCompactionUsageProvider provider = new(text => text.Length);

        OperationalContextCompactionUsage? usage = await provider.GetUsageAsync(
            [
                new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?> { ["value"] = 12345 })]),
            ],
            TestContext.CancellationToken);

        Assert.IsNotNull(usage);
        Assert.IsGreaterThan(0L, usage.UsedTokens);
    }
}
