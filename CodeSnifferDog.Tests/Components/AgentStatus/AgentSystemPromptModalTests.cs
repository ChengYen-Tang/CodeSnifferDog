using Bunit;
using CodeSnifferDog.Server.Client.Components.AgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Tests.Components.AgentStatus;

[TestClass]
public sealed class AgentSystemPromptModalTests
{
    [TestMethod]
    public void RendersModalForSelectedAgent()
    {
        using Bunit.TestContext context = new();
        ProjectAgentSnapshotDto agent = CreateAgent("Scan Agent", "Inspect repository boundaries.");

        IRenderedComponent<AgentSystemPromptModal> cut = context.RenderComponent<AgentSystemPromptModal>(
            parameters => parameters
                .Add(component => component.Agent, agent)
                .Add(component => component.SystemPrompt, agent.SystemPrompt));

        Assert.AreEqual(1, cut.FindAll("#agent-system-prompt-modal").Count);
        StringAssert.Contains(cut.Find("#agent-system-prompt-title").TextContent, "System Prompt");
        StringAssert.Contains(cut.Find(".agent-system-prompt-modal-subtitle").TextContent, "Scan Agent");
        StringAssert.Contains(cut.Find(".agent-system-prompt-content").TextContent, "Inspect repository boundaries.");
    }

    [TestMethod]
    public void RendersFallbackPromptTextWhenSystemPromptIsEmpty()
    {
        using Bunit.TestContext context = new();
        ProjectAgentSnapshotDto agent = CreateAgent("Agent", "");

        IRenderedComponent<AgentSystemPromptModal> cut = context.RenderComponent<AgentSystemPromptModal>(
            parameters => parameters
                .Add(component => component.Agent, agent)
                .Add(component => component.SystemPrompt, agent.SystemPrompt));

        StringAssert.Contains(cut.Find(".agent-system-prompt-content").TextContent, "No system prompt is available for this agent.");
    }

    [TestMethod]
    public void DoesNotRenderModalWhenNoAgentIsSelected()
    {
        using Bunit.TestContext context = new();

        IRenderedComponent<AgentSystemPromptModal> cut = context.RenderComponent<AgentSystemPromptModal>();

        Assert.IsEmpty(cut.FindAll("#agent-system-prompt-modal"));
    }

    private static ProjectAgentSnapshotDto CreateAgent(string displayName, string systemPrompt) => new()
    {
        AgentId = Guid.Parse("94000000-0000-0000-0000-000000000001"),
        GroupId = Guid.Parse("94000000-0000-0000-0000-000000000002"),
        RuntimeKey = displayName,
        DisplayName = displayName,
        SystemPrompt = systemPrompt,
        Status = ProjectAgentRunStatus.Waiting,
        CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        HasLoadedHistory = false,
        TimelineEntries = [],
    };
}
