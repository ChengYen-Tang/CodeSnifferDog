using CodeSnifferDog.Agents.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentToolComposerTests
{
    [TestMethod]
    public void Compose_PutsCommonToolsBeforeDomainTools()
    {
        AgentToolComposer composer = new();
        AITool domainTool = AIFunctionFactory.Create(() => true, "DomainTool", "Domain tool.", serializerOptions: null);

        IList<AITool> tools = composer.Compose(AppContext.BaseDirectory, [domainTool]);

        AssertToolNames(
            ["Shell", "Ripgrep", "ReadFileRange", "DomainTool"],
            tools);
    }

    [TestMethod]
    public void Compose_WithNoDomainTools_ReturnsCommonTools()
    {
        AgentToolComposer composer = new();

        IList<AITool> tools = composer.Compose(AppContext.BaseDirectory, []);

        AssertToolNames(
            ["Shell", "Ripgrep", "ReadFileRange"],
            tools);
    }

    [TestMethod]
    public void Compose_PreservesDomainToolMetadata()
    {
        AgentToolComposer composer = new();
        AITool domainTool = AIFunctionFactory.Create(() => true, "DomainTool", "Domain description.", serializerOptions: null);

        AITool composedDomainTool = composer.Compose(AppContext.BaseDirectory, [domainTool]).Last();

        Assert.AreEqual("DomainTool", composedDomainTool.Name);
        Assert.AreEqual("Domain description.", composedDomainTool.Description);
    }

    private static void AssertToolNames(IReadOnlyList<string> expectedToolNames, IEnumerable<AITool> tools)
    {
        string[] actualToolNames = tools.Select(tool => tool.Name).ToArray();
        Assert.IsTrue(
            expectedToolNames.SequenceEqual(actualToolNames, StringComparer.Ordinal),
            $"Expected tool names [{string.Join(", ", expectedToolNames)}], actual [{string.Join(", ", actualToolNames)}].");
    }
}
