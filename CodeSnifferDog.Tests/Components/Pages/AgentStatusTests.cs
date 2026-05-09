using Bunit;
using AngleSharp.Dom;
using CodeSnifferDog.Server.Components.Pages;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Components.Pages;

[TestClass]
public sealed class AgentStatusTests
{
    public required Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; init; }

    [TestMethod]
    public void RendersSnapshotAndSelectsFirstAvailableAgent()
    {
        using Bunit.TestContext context = new();
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000001"),
            groups:
            [
                CreateGroup(
                    groupId: Guid.Parse("71000000-0000-0000-0000-000000000001"),
                    runtimeKey: "group-a",
                    displayName: "Group A",
                    createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    agents:
                    [
                        CreateAgent(
                            agentId: Guid.Parse("72000000-0000-0000-0000-000000000001"),
                            groupId: Guid.Parse("71000000-0000-0000-0000-000000000001"),
                            runtimeKey: "agent-1",
                            displayName: "Rule Review Agent",
                            status: ProjectAgentRunStatus.Running,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000001"),
                                    agentId: Guid.Parse("72000000-0000-0000-0000-000000000001"),
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Input,
                                    message: "First agent input"),
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000002"),
                                    agentId: Guid.Parse("72000000-0000-0000-0000-000000000001"),
                                    sequence: 2,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "First agent output")
                            ]),
                        CreateAgent(
                            agentId: Guid.Parse("72000000-0000-0000-0000-000000000002"),
                            groupId: Guid.Parse("71000000-0000-0000-0000-000000000001"),
                            runtimeKey: "agent-2",
                            displayName: "Review Verifier Agent",
                            status: ProjectAgentRunStatus.Waiting,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
                            timeline: [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={snapshot.ProjectId}");

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Rule Review Agent");
            StringAssert.Contains(cut.Markup, "Review Verifier Agent");
            StringAssert.Contains(cut.Markup, "First agent input");
            StringAssert.Contains(cut.Markup, "First agent output");
            StringAssert.Contains(cut.Markup, "Snapshot loaded");
        });

        IElement selectedNode = cut.Find(".agent-roster-node.selected");
        StringAssert.Contains(selectedNode.TextContent, "Rule Review Agent");
    }

    [TestMethod]
    public void ReplacesSelectedAgentWhenSnapshotChangesAndOriginalAgentDisappears()
    {
        using Bunit.TestContext context = new();
        ProjectAgentStatusSnapshotDto firstSnapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000010"),
            groups:
            [
                CreateGroup(
                    groupId: Guid.Parse("71000000-0000-0000-0000-000000000010"),
                    runtimeKey: "group-a",
                    displayName: "Group A",
                    createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    agents:
                    [
                        CreateAgent(
                            agentId: Guid.Parse("72000000-0000-0000-0000-000000000010"),
                            groupId: Guid.Parse("71000000-0000-0000-0000-000000000010"),
                            runtimeKey: "agent-a",
                            displayName: "First Agent",
                            status: ProjectAgentRunStatus.Running,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000010"),
                                    agentId: Guid.Parse("72000000-0000-0000-0000-000000000010"),
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "First snapshot history")
                            ])
                    ])
            ]);

        ProjectAgentStatusSnapshotDto secondSnapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000011"),
            groups:
            [
                CreateGroup(
                    groupId: Guid.Parse("71000000-0000-0000-0000-000000000011"),
                    runtimeKey: "group-b",
                    displayName: "Group B",
                    createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 5, 0, TimeSpan.Zero),
                    agents:
                    [
                        CreateAgent(
                            agentId: Guid.Parse("72000000-0000-0000-0000-000000000011"),
                            groupId: Guid.Parse("71000000-0000-0000-0000-000000000011"),
                            runtimeKey: "agent-b",
                            displayName: "Replacement Agent",
                            status: ProjectAgentRunStatus.Waiting,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 6, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000011"),
                                    agentId: Guid.Parse("72000000-0000-0000-0000-000000000011"),
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "Second snapshot history")
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([firstSnapshot, secondSnapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={firstSnapshot.ProjectId}");

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "First Agent");
            StringAssert.Contains(cut.Markup, "First snapshot history");
        });

        cut.Instance.ProjectId = secondSnapshot.ProjectId;
        cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.Empty)).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Replacement Agent");
            StringAssert.Contains(cut.Markup, "Second snapshot history");
            Assert.DoesNotContain(cut.Markup, "First snapshot history");
        });

        IElement selectedNode = cut.Find(".agent-roster-node.selected");
        StringAssert.Contains(selectedNode.TextContent, "Replacement Agent");
    }

    [TestMethod]
    public void ShowsNoProjectSelectedWhenProjectIdIsMissing()
    {
        using Bunit.TestContext context = new();
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "No project selected.");
            StringAssert.Contains(cut.Markup, "Snapshot unavailable");
        });
    }

    [TestMethod]
    public void ShowsNotFoundErrorWhenSnapshotEndpointReturns404()
    {
        using Bunit.TestContext context = new();
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([], HttpStatusCode.NotFound))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/agent-status?projectId=70000000-0000-0000-0000-000000000099");

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Project snapshot was not found.");
            StringAssert.Contains(cut.Markup, "Snapshot unavailable");
        });
    }

    [TestMethod]
    public void KeepsSelectionEmptyWhenSnapshotContainsNoAgents()
    {
        using Bunit.TestContext context = new();
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000120"),
            groups:
            [
                CreateGroup(
                    groupId: Guid.Parse("71000000-0000-0000-0000-000000000120"),
                    runtimeKey: "empty-group",
                    displayName: "Empty Group",
                    createdAtUtc: new DateTimeOffset(2026, 5, 10, 11, 0, 0, TimeSpan.Zero),
                    agents: [])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={snapshot.ProjectId}");

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Snapshot loaded");
            StringAssert.Contains(cut.Markup, "Empty Group");
            Assert.IsEmpty(cut.FindAll(".agent-roster-node.selected"));
            Assert.IsEmpty(cut.FindAll(".agent-message"));
        });
    }

    [TestMethod]
    public void ShowsFailureErrorWhenSnapshotEndpointReturnsServerError()
    {
        using Bunit.TestContext context = new();
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([], HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/agent-status?projectId=70000000-0000-0000-0000-000000000121");

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Failed to load snapshot:");
            StringAssert.Contains(cut.Markup, "500");
            StringAssert.Contains(cut.Markup, "Snapshot unavailable");
        });
    }

    [TestMethod]
    public void KeepsSelectedAgentWhenSnapshotChangesAndAgentStillExists()
    {
        using Bunit.TestContext context = new();
        Guid selectedAgentId = Guid.Parse("72000000-0000-0000-0000-000000000020");
        Guid otherAgentId = Guid.Parse("72000000-0000-0000-0000-000000000021");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000020");

        ProjectAgentStatusSnapshotDto firstSnapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000020"),
            groups:
            [
                CreateGroup(
                    groupId: groupId,
                    runtimeKey: "group-a",
                    displayName: "Group A",
                    createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    agents:
                    [
                        CreateAgent(
                            agentId: selectedAgentId,
                            groupId: groupId,
                            runtimeKey: "selected-agent",
                            displayName: "Selected Agent",
                            status: ProjectAgentRunStatus.Running,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000020"),
                                    agentId: selectedAgentId,
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "Selected agent history")
                            ]),
                        CreateAgent(
                            agentId: otherAgentId,
                            groupId: groupId,
                            runtimeKey: "other-agent",
                            displayName: "Other Agent",
                            status: ProjectAgentRunStatus.Waiting,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000021"),
                                    agentId: otherAgentId,
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "Other agent history")
                            ])
                    ])
            ]);

        ProjectAgentStatusSnapshotDto secondSnapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000021"),
            groups:
            [
                CreateGroup(
                    groupId: groupId,
                    runtimeKey: "group-a",
                    displayName: "Group A",
                    createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    agents:
                    [
                        CreateAgent(
                            agentId: selectedAgentId,
                            groupId: groupId,
                            runtimeKey: "selected-agent",
                            displayName: "Selected Agent",
                            status: ProjectAgentRunStatus.Running,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000022"),
                                    agentId: selectedAgentId,
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "Selected agent refreshed history")
                            ]),
                        CreateAgent(
                            agentId: otherAgentId,
                            groupId: groupId,
                            runtimeKey: "other-agent",
                            displayName: "Other Agent",
                            status: ProjectAgentRunStatus.Waiting,
                            createdAtUtc: new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
                            timeline:
                            [
                                CreateTimelineEntry(
                                    timelineEntryId: Guid.Parse("73000000-0000-0000-0000-000000000023"),
                                    agentId: otherAgentId,
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "Other agent refreshed history")
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([firstSnapshot, secondSnapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={firstSnapshot.ProjectId}");

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Selected agent history");
        });

        IElement selectedNodeBefore = cut.Find(".agent-roster-node.selected");
        StringAssert.Contains(selectedNodeBefore.TextContent, "Selected Agent");

        cut.Instance.ProjectId = secondSnapshot.ProjectId;
        cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.Empty)).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Selected agent refreshed history");
            Assert.DoesNotContain(cut.Markup, "Other agent refreshed history");
        });

        IElement selectedNodeAfter = cut.Find(".agent-roster-node.selected");
        StringAssert.Contains(selectedNodeAfter.TextContent, "Selected Agent");
    }

    private static ProjectAgentStatusSnapshotDto CreateSnapshot(
        Guid projectId,
        IReadOnlyList<ProjectAgentGroupSnapshotDto> groups) => new()
        {
            ProjectId = projectId,
            ProjectStatus = ProjectStatus.Reviewing,
            SnapshotGeneratedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 30, 0, TimeSpan.Zero),
            AgentGroups = groups,
        };

    private static ProjectAgentGroupSnapshotDto CreateGroup(
        Guid groupId,
        string runtimeKey,
        string displayName,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<ProjectAgentSnapshotDto> agents) => new()
        {
            GroupId = groupId,
            RuntimeKey = runtimeKey,
            DisplayName = displayName,
            CreatedAtUtc = createdAtUtc,
            Agents = agents,
        };

    private static ProjectAgentSnapshotDto CreateAgent(
        Guid agentId,
        Guid groupId,
        string runtimeKey,
        string displayName,
        ProjectAgentRunStatus status,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<ProjectAgentTimelineEntryDto> timeline) => new()
        {
            AgentId = agentId,
            GroupId = groupId,
            RuntimeKey = runtimeKey,
            DisplayName = displayName,
            Status = status,
            CreatedAtUtc = createdAtUtc,
            TimelineEntries = timeline,
        };

    private static ProjectAgentTimelineEntryDto CreateTimelineEntry(
        Guid timelineEntryId,
        Guid agentId,
        long sequence,
        ProjectAgentTimelineEntryKind entryKind,
        string? message = null,
        string? toolCallId = null,
        string? toolName = null,
        string? toolArguments = null,
        string? toolResult = null) => new()
        {
            TimelineEntryId = timelineEntryId,
            AgentId = agentId,
            Sequence = sequence,
            EntryKind = entryKind,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 30, 0, TimeSpan.Zero),
            Message = message,
            ToolCallId = toolCallId,
            ToolName = toolName,
            ToolArguments = toolArguments,
            ToolResult = toolResult,
        };

    private sealed class SnapshotMessageHandler(
        IReadOnlyList<ProjectAgentStatusSnapshotDto> snapshots,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly Queue<ProjectAgentStatusSnapshotDto> _snapshots = new(snapshots);
        private readonly HttpStatusCode _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_statusCode != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode));
            }

            if (_snapshots.Count == 0)
                throw new InvalidOperationException("No snapshot response was configured.");

            ProjectAgentStatusSnapshotDto snapshot = _snapshots.Dequeue();
            string json = JsonSerializer.Serialize(snapshot);

            HttpResponseMessage response = new(_statusCode)
            {
                Content = new StringContent(json),
            };

            return Task.FromResult(response);
        }
    }
}
