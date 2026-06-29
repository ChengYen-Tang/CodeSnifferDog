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

        CollectionAssert.AreEqual(
            new[] { "RunShellCommand", "RunRipgrepCommand", "DomainTool" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void Compose_WithNoDomainTools_ReturnsCommonTools()
    {
        AgentToolComposer composer = new();

        IList<AITool> tools = composer.Compose(AppContext.BaseDirectory, []);

        CollectionAssert.AreEqual(
            new[] { "RunShellCommand", "RunRipgrepCommand" },
            tools.Select(tool => tool.Name).ToArray());
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
}
