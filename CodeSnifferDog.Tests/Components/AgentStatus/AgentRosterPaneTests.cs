using Bunit;
using CodeSnifferDog.Server.Client.Components.AgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.AspNetCore.Components;

namespace CodeSnifferDog.Tests.Components.AgentStatus;

[TestClass]
public sealed class AgentRosterPaneTests
{
    [TestMethod]
    public void RendersGroupsAgentsAndSelectedState()
    {
        using Bunit.TestContext context = new();
        Guid groupId = Guid.Parse("91000000-0000-0000-0000-000000000001");
        Guid selectedAgentId = Guid.Parse("91000000-0000-0000-0000-000000000002");
        IReadOnlyList<ProjectAgentGroupSnapshotDto> groups =
        [
            CreateGroup(
                groupId,
                "Review Group",
                [
                    CreateAgent(selectedAgentId, groupId, "Selected Agent", ProjectAgentRunStatus.Running),
                    CreateAgent(Guid.Parse("91000000-0000-0000-0000-000000000003"), groupId, "Waiting Agent", ProjectAgentRunStatus.Waiting),
                ])
        ];

        IRenderedComponent<AgentRosterPane> cut = context.RenderComponent<AgentRosterPane>(
            parameters => parameters
                .Add(component => component.Groups, groups)
                .Add(component => component.SelectedAgentId, selectedAgentId));

        Assert.AreEqual(1, cut.FindAll(".agent-group-card").Count);
        Assert.AreEqual(2, cut.FindAll(".agent-roster-node").Count);
        StringAssert.Contains(cut.Find(".agent-roster-node.selected").TextContent, "Selected Agent");
        Assert.AreEqual("Running", cut.Find(".agent-roster-node.selected .agent-status-dot").GetAttribute("title"));
    }

    [TestMethod]
    public void ClickingAgentRaisesSelectedAgentCallback()
    {
        using Bunit.TestContext context = new();
        Guid groupId = Guid.Parse("91000000-0000-0000-0000-000000000011");
        Guid agentId = Guid.Parse("91000000-0000-0000-0000-000000000012");
        Guid? selectedAgentId = null;

        IRenderedComponent<AgentRosterPane> cut = context.RenderComponent<AgentRosterPane>(
            parameters => parameters
                .Add(component => component.Groups, [CreateGroup(groupId, "Group", [CreateAgent(agentId, groupId, "Agent", ProjectAgentRunStatus.Waiting)])])
                .Add(component => component.OnSelectAgent, EventCallback.Factory.Create<Guid>(this, value => selectedAgentId = value)));

        cut.Find(".agent-roster-node").Click();

        Assert.AreEqual(agentId, selectedAgentId);
    }

    [TestMethod]
    public void LargeRoster_RendersAllAgents()
    {
        using Bunit.TestContext context = new();
        IReadOnlyList<ProjectAgentGroupSnapshotDto> groups = Enumerable.Range(0, 20)
            .Select(groupIndex =>
            {
                Guid groupId = Guid.Parse($"91000000-0000-0001-0000-{groupIndex:000000000000}");
                return CreateGroup(
                    groupId,
                    $"Group {groupIndex}",
                    Enumerable.Range(0, 10)
                        .Select(agentIndex => CreateAgent(
                            Guid.Parse($"91000000-0000-0002-{groupIndex:0000}-{agentIndex:000000000000}"),
                            groupId,
                            $"Agent {groupIndex}-{agentIndex}",
                            ProjectAgentRunStatus.Waiting))
                        .ToList());
            })
            .ToList();

        IRenderedComponent<AgentRosterPane> cut = context.RenderComponent<AgentRosterPane>(
            parameters => parameters.Add(component => component.Groups, groups));

        Assert.AreEqual(20, cut.FindAll(".agent-group-card").Count);
        Assert.AreEqual(200, cut.FindAll(".agent-roster-node").Count);
    }

    private static ProjectAgentGroupSnapshotDto CreateGroup(
        Guid groupId,
        string displayName,
        IReadOnlyList<ProjectAgentSnapshotDto> agents) => new()
        {
            GroupId = groupId,
            RuntimeKey = displayName,
            DisplayName = displayName,
            CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            Agents = agents,
        };

    private static ProjectAgentSnapshotDto CreateAgent(
        Guid agentId,
        Guid groupId,
        string displayName,
        ProjectAgentRunStatus status) => new()
        {
            AgentId = agentId,
            GroupId = groupId,
            RuntimeKey = displayName,
            DisplayName = displayName,
            SystemPrompt = "",
            Status = status,
            CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 10, 1, 0, TimeSpan.Zero),
            HasLoadedHistory = false,
            TimelineEntries = [],
        };
}
