using Bunit;
using CodeSnifferDog.Server.Client.Components.AgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Tests.Components.AgentStatus;

[TestClass]
public sealed class AgentHistoryPaneTests
{
    [TestMethod]
    public void RendersLoadingAndErrorStatesForSelectedAgent()
    {
        using Bunit.TestContext context = new();
        Guid agentId = Guid.Parse("93000000-0000-0000-0000-000000000001");

        IRenderedComponent<AgentHistoryPane> loading = context.RenderComponent<AgentHistoryPane>(
            parameters => parameters
                .Add(component => component.SelectedAgentId, agentId)
                .Add(component => component.HistoryAgentId, agentId)
                .Add(component => component.IsHistoryLoading, true));
        IRenderedComponent<AgentHistoryPane> error = context.RenderComponent<AgentHistoryPane>(
            parameters => parameters
                .Add(component => component.SelectedAgentId, agentId)
                .Add(component => component.HistoryAgentId, agentId)
                .Add(component => component.HistoryErrorMessage, "History failed"));

        StringAssert.Contains(loading.Markup, "Loading agent history...");
        StringAssert.Contains(error.Markup, "History failed");
    }

    [TestMethod]
    public void RendersSelectedAgentTimelineAndPromptButton()
    {
        using Bunit.TestContext context = new();
        Guid agentId = Guid.Parse("93000000-0000-0000-0000-000000000011");
        ProjectAgentSnapshotDto agent = CreateAgent(agentId, "Selected Agent");
        IReadOnlyList<ProjectAgentTimelineEntryDto> timeline =
        [
            CreateEntry(agentId, "Selected history")
        ];

        IRenderedComponent<AgentHistoryPane> cut = context.RenderComponent<AgentHistoryPane>(
            parameters => parameters
                .Add(component => component.SelectedAgent, agent)
                .Add(component => component.SelectedAgentId, agentId)
                .Add(component => component.HistoryAgentId, agentId)
                .Add(component => component.TimelineEntries, timeline));

        StringAssert.Contains(cut.Find(".agent-history-toolbar").TextContent, "Selected Agent");
        Assert.IsFalse(cut.Find(".agent-system-prompt-button").HasAttribute("disabled"));
        StringAssert.Contains(cut.Markup, "Selected history");
    }

    [TestMethod]
    public void DisablesPromptButtonWhenNoAgentIsSelected()
    {
        using Bunit.TestContext context = new();

        IRenderedComponent<AgentHistoryPane> cut = context.RenderComponent<AgentHistoryPane>();

        Assert.IsTrue(cut.Find(".agent-system-prompt-button").HasAttribute("disabled"));
    }

    private static ProjectAgentSnapshotDto CreateAgent(Guid agentId, string displayName) => new()
    {
        AgentId = agentId,
        GroupId = Guid.Parse("93000000-0000-0000-0000-000000000012"),
        RuntimeKey = displayName,
        DisplayName = displayName,
        SystemPrompt = "",
        Status = ProjectAgentRunStatus.Running,
        CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        HasLoadedHistory = true,
        TimelineEntries = [],
    };

    private static ProjectAgentTimelineEntryDto CreateEntry(Guid agentId, string message) => new()
    {
        TimelineEntryId = Guid.Parse("93000000-0000-0000-0000-000000000013"),
        AgentId = agentId,
        Sequence = 1,
        EntryKind = ProjectAgentTimelineEntryKind.Output,
        OccurredAtUtc = new DateTimeOffset(2026, 6, 1, 10, 1, 0, TimeSpan.Zero),
        Message = message,
    };
}
