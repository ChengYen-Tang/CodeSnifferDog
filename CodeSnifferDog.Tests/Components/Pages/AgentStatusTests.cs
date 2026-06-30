using Bunit;
using AngleSharp.Dom;
using AgentStatusPage = CodeSnifferDog.Server.Client.Pages.AgentStatus;
using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Reflection;
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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

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
    public void SystemPromptButtonTargetsSelectedAgentPromptModal()
    {
        using Bunit.TestContext context = new();
        RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000300");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000300");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000300");
        string systemPrompt = "You are the scan agent.\nInspect repository boundaries.";
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Scan Agent",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000300"),
                                    agentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "Scanning")
                            ],
                            systemPrompt)
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Scan Agent"));
        IElement promptButton = cut.Find(".agent-system-prompt-button");
        Assert.AreEqual("modal", promptButton.GetAttribute("data-bs-toggle"));
        Assert.AreEqual("#agent-system-prompt-modal", promptButton.GetAttribute("data-bs-target"));

        IElement modal = cut.Find("#agent-system-prompt-modal");
        Assert.Contains("modal", modal.ClassList);
        StringAssert.Contains(modal.TextContent, "System Prompt");
        StringAssert.Contains(modal.TextContent, "Scan Agent");
        StringAssert.Contains(modal.TextContent, systemPrompt);

        IElement closeButton = cut.Find(".agent-system-prompt-close");
        Assert.AreEqual("modal", closeButton.GetAttribute("data-bs-dismiss"));
    }

    [TestMethod]
    public void ReplacesSelectedAgentWhenSnapshotChangesAndOriginalAgentDisappears()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([], statusCode: HttpStatusCode.NotFound))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/agent-status?projectId=70000000-0000-0000-0000-000000000099");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([], statusCode: HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/agent-status?projectId=70000000-0000-0000-0000-000000000121");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Failed to load snapshot:");
            StringAssert.Contains(cut.Markup, "500");
            StringAssert.Contains(cut.Markup, "Snapshot unavailable");
        });
    }

    [TestMethod]
    public void LargeSnapshot_RendersRosterAndSelectedTimeline()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("90000000-0000-0000-0000-000000000100");
        Guid selectedGroupId = Guid.Parse("90000000-0000-0000-0000-000000000200");
        Guid selectedAgentId = Guid.Parse("90000000-0000-0000-0000-000000000300");
        List<ProjectAgentTimelineEntryDto> selectedTimeline = Enumerable.Range(1, 500)
            .Select(index => CreateTimelineEntry(
                Guid.Parse($"90000000-0000-0000-0001-{index:000000000000}"),
                selectedAgentId,
                index,
                ProjectAgentTimelineEntryKind.Output,
                message: $"Selected timeline entry {index}"))
            .ToList();
        List<ProjectAgentGroupSnapshotDto> groups = Enumerable.Range(0, 20)
            .Select(groupIndex =>
            {
                Guid groupId = groupIndex == 0
                    ? selectedGroupId
                    : Guid.Parse($"90000000-0000-0000-0002-{groupIndex:000000000000}");
                List<ProjectAgentSnapshotDto> agents = Enumerable.Range(0, 10)
                    .Select(agentIndex =>
                    {
                        Guid agentId = groupIndex == 0 && agentIndex == 0
                            ? selectedAgentId
                            : Guid.Parse($"90000000-0000-{groupIndex:0000}-{agentIndex:0000}-000000000000");
                        return CreateAgent(
                            agentId,
                            groupId,
                            $"agent-{groupIndex}-{agentIndex}",
                            $"Agent {groupIndex}-{agentIndex}",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 10, groupIndex, agentIndex, TimeSpan.Zero),
                            groupIndex == 0 && agentIndex == 0 ? selectedTimeline : []);
                    })
                    .ToList();

                return CreateGroup(
                    groupId,
                    $"group-{groupIndex}",
                    $"Group {groupIndex}",
                    new DateTimeOffset(2026, 5, 10, 9, groupIndex, 0, TimeSpan.Zero),
                    agents);
            })
            .ToList();

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(projectId, groups);
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(20, cut.FindAll(".agent-group-card").Count);
            Assert.AreEqual(200, cut.FindAll(".agent-roster-node").Count);
            Assert.AreEqual(500, cut.FindAll(".agent-message").Count);
            StringAssert.Contains(cut.Markup, "Selected timeline entry 500");
            Assert.IsTrue(liveSubscriptionClient.SubscribeCalls.Count > 0);
        });
    }

    [TestMethod]
    public void NoOpLiveUpdate_DoesNotRequestRender()
    {
        using Bunit.TestContext context = new();
        RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("90000000-0000-0000-0000-000000000110");
        Guid groupId = Guid.Parse("90000000-0000-0000-0000-000000000111");
        Guid agentId = Guid.Parse("90000000-0000-0000-0000-000000000112");
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-1",
                    "Group 1",
                    new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-1",
                            "Agent 1",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");
        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Agent 1"));

        bool changed = InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = projectId,
                Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
                TimelineEntry = CreateTimelineEntry(
                    Guid.Parse("90000000-0000-0000-0000-000000000113"),
                    Guid.Parse("90000000-0000-0000-0000-000000000114"),
                    1,
                    ProjectAgentTimelineEntryKind.Output,
                    message: "This update belongs to another agent"),
            });

        Assert.IsFalse(changed);
        Assert.DoesNotContain(cut.Markup, "This update belongs to another agent");
    }

    [TestMethod]
    public void NonSelectedAgentStatusUpdate_UpdatesRosterWithoutChangingSelectedTimeline()
    {
        using Bunit.TestContext context = new();
        RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("90000000-0000-0000-0000-000000000120");
        Guid groupId = Guid.Parse("90000000-0000-0000-0000-000000000121");
        Guid selectedAgentId = Guid.Parse("90000000-0000-0000-0000-000000000122");
        Guid otherAgentId = Guid.Parse("90000000-0000-0000-0000-000000000123");
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-1",
                    "Group 1",
                    new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            selectedAgentId,
                            groupId,
                            "selected-agent",
                            "Selected Agent",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("90000000-0000-0000-0000-000000000124"),
                                    selectedAgentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "Selected timeline remains visible")
                            ]),
                        CreateAgent(
                            otherAgentId,
                            groupId,
                            "other-agent",
                            "Other Agent",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);
        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");
        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Selected timeline remains visible");
            Assert.AreEqual("Waiting", cut.FindAll(".agent-status-dot")[1].GetAttribute("title"));
        });

        bool changed = InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = projectId,
                Kind = ProjectAgentLiveUpdateKind.AgentStatusChanged,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 3, 0, TimeSpan.Zero),
                AgentStatus = new ProjectAgentStatusChangedDto
                {
                    AgentId = otherAgentId,
                    Status = ProjectAgentRunStatus.Completed,
                    OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 3, 0, TimeSpan.Zero),
                },
            });

        Assert.IsTrue(changed);
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Selected timeline remains visible");
            Assert.AreEqual(1, cut.FindAll(".agent-message").Count);
            Assert.AreEqual("Completed", cut.FindAll(".agent-status-dot")[1].GetAttribute("title"));
        });
    }

    [TestMethod]
    public void KeepsSelectedAgentWhenSnapshotChangesAndAgentStillExists()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

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

    [TestMethod]
    public void SelectingAgentWithoutPreloadedHistoryFetchesHistoryOnDemand()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000301");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000301");
        Guid selectedAgentId = Guid.Parse("72000000-0000-0000-0000-000000000301");
        Guid unloadedAgentId = Guid.Parse("72000000-0000-0000-0000-000000000302");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            selectedAgentId,
                            groupId,
                            "agent-a",
                            "Selected Agent",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000301"),
                                    selectedAgentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "Selected history")
                            ]),
                        CreateAgent(
                            unloadedAgentId,
                            groupId,
                            "agent-b",
                            "Lazy Agent",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler(
            [snapshot],
            new Dictionary<Guid, ProjectAgentHistorySnapshotDto>
            {
                [unloadedAgentId] = new()
                {
                    ProjectId = projectId,
                    AgentId = unloadedAgentId,
                    TimelineEntries =
                    [
                        CreateTimelineEntry(
                            Guid.Parse("73000000-0000-0000-0000-000000000302"),
                            unloadedAgentId,
                            1,
                            ProjectAgentTimelineEntryKind.Output,
                            message: "Lazy history")
                    ],
                }
            }))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Selected history"));

        cut.FindAll(".agent-roster-node")[1].Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Lazy Agent");
            StringAssert.Contains(cut.Markup, "Lazy history");
            Assert.DoesNotContain(cut.Markup, "Selected history");
        });
    }

    [TestMethod]
    public void LiveReducer_AddsGroupAndAgentAndSelectsFirstAvailableAgent()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId: Guid.Parse("70000000-0000-0000-0000-000000000200"),
            groups: []);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={snapshot.ProjectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Snapshot loaded"));

        InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = snapshot.ProjectId,
                Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
                Group = new ProjectAgentGroupLiveDto
                {
                    GroupId = Guid.Parse("71000000-0000-0000-0000-000000000200"),
                    RuntimeKey = "live-group",
                    DisplayName = "Live Group",
                    CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
                },
            });

        InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = snapshot.ProjectId,
                Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                Agent = new ProjectAgentLiveDto
                {
                    AgentId = Guid.Parse("72000000-0000-0000-0000-000000000200"),
                    GroupId = Guid.Parse("71000000-0000-0000-0000-000000000200"),
                    RuntimeKey = "live-agent",
                    DisplayName = "Live Agent",
                    Status = ProjectAgentRunStatus.Waiting,
                    CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                },
            });

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Live Group");
            StringAssert.Contains(cut.Markup, "Live Agent");
        });

        IElement selectedNode = cut.Find(".agent-roster-node.selected");
        StringAssert.Contains(selectedNode.TextContent, "Live Agent");
    }

    [TestMethod]
    public void LiveReducer_StatusUpdateIsIdempotentAndDoesNotDuplicateAgent()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000201");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000201");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000201");
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Status Agent",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Status Agent"));

        ProjectAgentLiveUpdateDto statusUpdate = new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentStatusChanged,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
            AgentStatus = new ProjectAgentStatusChangedDto
            {
                AgentId = agentId,
                Status = ProjectAgentRunStatus.Running,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
            },
        };

        InvokeApplyLiveUpdate(cut, statusUpdate);
        InvokeApplyLiveUpdate(cut, statusUpdate);

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, cut.FindAll(".agent-roster-node"));
            StringAssert.Contains(cut.Markup, "Status Agent");
            Assert.AreEqual("Running", cut.Find(".agent-status-dot").GetAttribute("title"));
        });
    }

    [TestMethod]
    public void LiveReducer_ToolTimelineUpdateIsUpsertNotAppend()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000202");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000202");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000202");
        Guid timelineEntryId = Guid.Parse("73000000-0000-0000-0000-000000000202");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Tool Agent",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Tool Agent"));

        InvokeApplyLiveUpdate(
            cut,
            CreateTimelineLiveUpdate(
                projectId,
                timelineEntryId,
                agentId,
                sequence: 1,
                entryKind: ProjectAgentTimelineEntryKind.Tool,
                occurredAtUtc: new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
                toolCallId: "call-1",
                toolResult: "Created issue"));

        InvokeApplyLiveUpdate(
            cut,
            CreateTimelineLiveUpdate(
                projectId,
                timelineEntryId,
                agentId,
                sequence: 1,
                entryKind: ProjectAgentTimelineEntryKind.Tool,
                occurredAtUtc: new DateTimeOffset(2026, 5, 10, 12, 2, 1, TimeSpan.Zero),
                toolCallId: "call-1",
                toolName: "CreateIssue",
                toolArguments: "{ \"severity\": \"high\" }",
                toolResult: "Created issue"));

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, cut.FindAll(".tool-call-summary"));
            StringAssert.Contains(cut.Markup, "CreateIssue");
        });

        cut.Find(".tool-call-summary").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, cut.FindAll(".tool-call-summary"));
            StringAssert.Contains(cut.Markup, "{ \"severity\": \"high\" }");
            StringAssert.Contains(cut.Markup, "Created issue");
        });
    }

    [TestMethod]
    public void LiveReducer_TimelineEntriesRemoved_RemovesSelectedAgentHistoryEntries()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000206");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000206");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000206");
        Guid inputEntryId = Guid.Parse("73000000-0000-0000-0000-000000000206");
        Guid outputEntryId = Guid.Parse("74000000-0000-0000-0000-000000000206");
        Guid toolEntryId = Guid.Parse("75000000-0000-0000-0000-000000000206");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Retry Agent",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    inputEntryId,
                                    agentId,
                                    sequence: 1,
                                    entryKind: ProjectAgentTimelineEntryKind.Input,
                                    message: "Inspect Program.cs"),
                                CreateTimelineEntry(
                                    outputEntryId,
                                    agentId,
                                    sequence: 2,
                                    entryKind: ProjectAgentTimelineEntryKind.Output,
                                    message: "Failed attempt output"),
                                CreateTimelineEntry(
                                    toolEntryId,
                                    agentId,
                                    sequence: 3,
                                    entryKind: ProjectAgentTimelineEntryKind.Tool,
                                    toolCallId: "call-1",
                                    toolName: "RunShellCommand"),
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Inspect Program.cs");
            StringAssert.Contains(cut.Markup, "Failed attempt output");
            StringAssert.Contains(cut.Markup, "RunShellCommand");
        });

        InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = projectId,
                Kind = ProjectAgentLiveUpdateKind.TimelineEntriesRemoved,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
                RemovedTimelineEntries = new ProjectAgentTimelineEntriesRemovedDto
                {
                    AgentId = agentId,
                    TimelineEntryIds = [outputEntryId, toolEntryId],
                },
            });

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Inspect Program.cs");
            Assert.IsFalse(cut.Markup.Contains("Failed attempt output", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("RunShellCommand", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void LiveReducer_UnknownGroupOrAgentEvents_AreIgnoredWithoutBreakingSelection()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000203");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000203");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000203");
        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Primary Agent",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000203"),
                                    agentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "Baseline history")
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Primary Agent");
            StringAssert.Contains(cut.Markup, "Baseline history");
        });

        InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = projectId,
                Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
                Agent = new ProjectAgentLiveDto
                {
                    AgentId = Guid.Parse("72000000-0000-0000-0000-000000000299"),
                    GroupId = Guid.Parse("71000000-0000-0000-0000-000000000299"),
                    RuntimeKey = "missing-group-agent",
                    DisplayName = "Should Be Ignored",
                    Status = ProjectAgentRunStatus.Waiting,
                    CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
                },
            });

        InvokeApplyLiveUpdate(
            cut,
            new ProjectAgentLiveUpdateDto
            {
                ProjectId = projectId,
                Kind = ProjectAgentLiveUpdateKind.AgentStatusChanged,
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 1, TimeSpan.Zero),
                AgentStatus = new ProjectAgentStatusChangedDto
                {
                    AgentId = Guid.Parse("72000000-0000-0000-0000-000000000299"),
                    Status = ProjectAgentRunStatus.Degraded,
                    OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 12, 2, 1, TimeSpan.Zero),
                },
            });

        InvokeApplyLiveUpdate(
            cut,
            CreateTimelineLiveUpdate(
                projectId,
                Guid.Parse("73000000-0000-0000-0000-000000000299"),
                Guid.Parse("72000000-0000-0000-0000-000000000299"),
                sequence: 2,
                entryKind: ProjectAgentTimelineEntryKind.Output,
                occurredAtUtc: new DateTimeOffset(2026, 5, 10, 12, 2, 2, TimeSpan.Zero),
                message: "Should also be ignored"));

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, cut.FindAll(".agent-roster-node"));
            Assert.HasCount(1, cut.FindAll(".agent-roster-node.selected"));
            StringAssert.Contains(cut.Markup, "Primary Agent");
            StringAssert.Contains(cut.Markup, "Baseline history");
            Assert.DoesNotContain(cut.Markup, "Should Be Ignored");
            Assert.DoesNotContain(cut.Markup, "Should also be ignored");
        });
    }

    [TestMethod]
    public void SnapshotLoad_SubscribesToLiveUpdatesWithSelectedAgentCursor()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000204");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000204");
        Guid agentAId = Guid.Parse("72000000-0000-0000-0000-000000000204");
        Guid agentBId = Guid.Parse("72000000-0000-0000-0000-000000000205");
        DateTimeOffset snapshotGeneratedAtUtc = new(2026, 5, 10, 13, 30, 0, TimeSpan.Zero);

        ProjectAgentStatusSnapshotDto snapshot = new()
        {
            ProjectId = projectId,
            ProjectStatus = ProjectStatus.Reviewing,
            SnapshotGeneratedAtUtc = snapshotGeneratedAtUtc,
            AgentGroups =
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentAId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 13, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000204"),
                                    agentAId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "A1"),
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000205"),
                                    agentAId,
                                    3,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "A3")
                            ]),
                        CreateAgent(
                            agentBId,
                            groupId,
                            "agent-b",
                            "Agent B",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 13, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ],
        };

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls);
            Assert.IsNotNull(liveSubscriptionClient.LastRequest);
            Assert.AreEqual(projectId, liveSubscriptionClient.LastRequest.ProjectId);
            Assert.AreEqual(snapshotGeneratedAtUtc, liveSubscriptionClient.LastRequest.SnapshotGeneratedAtUtc);
            Assert.AreEqual(agentAId, liveSubscriptionClient.LastRequest.AgentId);
            Assert.AreEqual(3L, liveSubscriptionClient.LastRequest.LatestSequence);
        });

        liveSubscriptionClient.Emit(new ProjectAgentLiveUpdateDto
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 13, 31, 0, TimeSpan.Zero),
            TimelineEntry = CreateTimelineEntry(
                Guid.Parse("73000000-0000-0000-0000-000000000206"),
                agentAId,
                4,
                ProjectAgentTimelineEntryKind.Output,
                message: "Backfill A4")
        });

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Backfill A4"));
    }

    [TestMethod]
    public void SelectingAnotherAgent_ReSubscribesLiveUpdatesForSelectedAgentOnly()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000214");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000214");
        Guid agentAId = Guid.Parse("72000000-0000-0000-0000-000000000214");
        Guid agentBId = Guid.Parse("72000000-0000-0000-0000-000000000215");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentAId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 13, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000214"),
                                    agentAId,
                                    2,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "A2")
                            ]),
                        CreateAgent(
                            agentBId,
                            groupId,
                            "agent-b",
                            "Agent B",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 13, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler(
            [snapshot],
            new Dictionary<Guid, ProjectAgentHistorySnapshotDto>
            {
                [agentBId] = new()
                {
                    ProjectId = projectId,
                    AgentId = agentBId,
                    TimelineEntries =
                    [
                        CreateTimelineEntry(
                            Guid.Parse("73000000-0000-0000-0000-000000000215"),
                            agentBId,
                            5,
                            ProjectAgentTimelineEntryKind.Output,
                            message: "B5")
                    ],
                }
            }))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls);
            Assert.AreEqual(agentAId, liveSubscriptionClient.LastRequest?.AgentId);
            Assert.AreEqual(2L, liveSubscriptionClient.LastRequest?.LatestSequence);
        });

        cut.FindAll(".agent-roster-node")[1].Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(agentBId, liveSubscriptionClient.LastRequest?.AgentId);
            Assert.AreEqual(5L, liveSubscriptionClient.LastRequest?.LatestSequence);
            StringAssert.Contains(cut.Markup, "B5");
        });
    }

    [TestMethod]
    public void SelectingAnotherAgent_WhenHistoryLoadFails_UnsubscribesOldLiveTailAndShowsError()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000218");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000218");
        Guid agentAId = Guid.Parse("72000000-0000-0000-0000-000000000218");
        Guid agentBId = Guid.Parse("72000000-0000-0000-0000-000000000219");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentAId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 13, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000218"),
                                    agentAId,
                                    2,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "A2")
                            ]),
                        CreateAgent(
                            agentBId,
                            groupId,
                            "agent-b",
                            "Agent B",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 13, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls);
            Assert.AreEqual(agentAId, liveSubscriptionClient.LastRequest?.AgentId);
            StringAssert.Contains(cut.Markup, "A2");
        });

        cut.FindAll(".agent-roster-node")[1].Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, liveSubscriptionClient.UnsubscribeCallCount);
            Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls);
            StringAssert.Contains(cut.FindAll(".agent-roster-node.selected").Single().TextContent, "Agent B");
            StringAssert.Contains(cut.Markup, "Failed to load agent history: No history response was configured for agent");
            Assert.DoesNotContain(cut.Markup, "A2");
        });
    }

    [TestMethod]
    public void SelectingAnotherAgent_ClearsExpandedToolDetailsForPreviousAgent()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000220");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000220");
        Guid agentAId = Guid.Parse("72000000-0000-0000-0000-000000000220");
        Guid agentBId = Guid.Parse("72000000-0000-0000-0000-000000000221");

        ProjectAgentTimelineEntryDto agentAToolEntry = CreateTimelineEntry(
            Guid.Parse("73000000-0000-0000-0000-000000000220"),
            agentAId,
            1,
            ProjectAgentTimelineEntryKind.Tool,
            toolCallId: "tool-a",
            toolName: "ToolA",
            toolArguments: "arg-a",
            toolResult: "result-a");

        ProjectAgentTimelineEntryDto agentBToolEntry = CreateTimelineEntry(
            Guid.Parse("73000000-0000-0000-0000-000000000221"),
            agentBId,
            1,
            ProjectAgentTimelineEntryKind.Tool,
            toolCallId: "tool-b",
            toolName: "ToolB",
            toolArguments: "arg-b",
            toolResult: "result-b");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentAId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 13, 1, 0, TimeSpan.Zero),
                            [agentAToolEntry]),
                        CreateAgent(
                            agentBId,
                            groupId,
                            "agent-b",
                            "Agent B",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 13, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler(
            [snapshot],
            new Dictionary<Guid, ProjectAgentHistorySnapshotDto>
            {
                [agentAId] = new()
                {
                    ProjectId = projectId,
                    AgentId = agentAId,
                    TimelineEntries = [agentAToolEntry],
                },
                [agentBId] = new()
                {
                    ProjectId = projectId,
                    AgentId = agentBId,
                    TimelineEntries = [agentBToolEntry],
                }
            }))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "ToolA"));

        cut.Find(".tool-call-summary").Click();

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "arg-a"));

        cut.FindAll(".agent-roster-node")[1].Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "ToolB");
            Assert.DoesNotContain(cut.Markup, "arg-a");
        });

        cut.FindAll(".agent-roster-node")[0].Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "ToolA");
            Assert.DoesNotContain(cut.Markup, "arg-a");
            Assert.DoesNotContain(cut.Markup, "result-a");
        });
    }

    [TestMethod]
    public void RefreshingPage_RebuildsSelectedAgentStateAndResubscribesFromFreshSnapshot()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000216");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000216");
        Guid agentAId = Guid.Parse("72000000-0000-0000-0000-000000000216");
        Guid agentBId = Guid.Parse("72000000-0000-0000-0000-000000000217");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentAId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 13, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000216"),
                                    agentAId,
                                    2,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "A2")
                            ]),
                        CreateAgent(
                            agentBId,
                            groupId,
                            "agent-b",
                            "Agent B",
                            ProjectAgentRunStatus.Waiting,
                            new DateTimeOffset(2026, 5, 10, 13, 2, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler(
            [snapshot, snapshot],
            new Dictionary<Guid, ProjectAgentHistorySnapshotDto>
            {
                [agentBId] = new()
                {
                    ProjectId = projectId,
                    AgentId = agentBId,
                    TimelineEntries =
                    [
                        CreateTimelineEntry(
                            Guid.Parse("73000000-0000-0000-0000-000000000217"),
                            agentBId,
                            5,
                            ProjectAgentTimelineEntryKind.Output,
                            message: "B5")
                    ],
                }
            }))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> firstRender = RenderAgentStatus(context);

        firstRender.WaitForAssertion(() =>
        {
            StringAssert.Contains(firstRender.Markup, "A2");
            Assert.AreEqual(agentAId, liveSubscriptionClient.LastRequest?.AgentId);
            Assert.AreEqual(2L, liveSubscriptionClient.LastRequest?.LatestSequence);
        });

        firstRender.FindAll(".agent-roster-node")[1].Click();

        firstRender.WaitForAssertion(() =>
        {
            StringAssert.Contains(firstRender.Markup, "B5");
            Assert.AreEqual(agentBId, liveSubscriptionClient.LastRequest?.AgentId);
            Assert.AreEqual(5L, liveSubscriptionClient.LastRequest?.LatestSequence);
        });

        firstRender.Dispose();

        IRenderedComponent<AgentStatusPage> refreshedRender = RenderAgentStatus(context);

        refreshedRender.WaitForAssertion(() =>
        {
            StringAssert.Contains(refreshedRender.Markup, "A2");
            Assert.DoesNotContain(refreshedRender.Markup, "B5");
            Assert.AreEqual(agentAId, liveSubscriptionClient.LastRequest?.AgentId);
            Assert.AreEqual(2L, liveSubscriptionClient.LastRequest?.LatestSequence);
            Assert.HasCount(3, liveSubscriptionClient.SubscribeCalls);
        });
    }

    [TestMethod]
    public void SnapshotLoad_WhenLiveSubscriptionFails_KeepsSnapshotVisible()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        liveSubscriptionClient.SubscribeException = new InvalidOperationException("SignalR handshake failed");

        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000205");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000205");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000206");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 14, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 14, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000207"),
                                    agentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "Existing history")
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Agent A");
            StringAssert.Contains(cut.Markup, "Existing history");
            StringAssert.Contains(cut.Markup, "Failed to connect live updates: SignalR handshake failed");
            StringAssert.Contains(cut.Markup, "Live disconnected");
            Assert.DoesNotContain(cut.Markup, "Failed to load snapshot:");
        });
    }

    [TestMethod]
    public void ShowsCompletedCompletionState_WhenSnapshotIsCompleted()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000208");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000208");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000209");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 17, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Completed,
                            new DateTimeOffset(2026, 5, 10, 17, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ],
            projectStatus: ProjectStatus.Completed);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Analysis completed");
            StringAssert.Contains(cut.Markup, "Project execution finished successfully.");
        });
    }

    [TestMethod]
    public void ShowsFailedCompletionState_WhenSnapshotIsFailed()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000209");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000209");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000210");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Degraded,
                            new DateTimeOffset(2026, 5, 10, 18, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ],
            projectStatus: ProjectStatus.Failed);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Analysis failed");
            StringAssert.Contains(cut.Markup, "Project execution ended with a failure.");
        });
    }

    [TestMethod]
    public void LiveProjectStatusUpdate_TransitionsFromRunningToCompleted()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000210");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000210");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000211");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 19, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 19, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Analysis running"));

        liveSubscriptionClient.Emit(new ProjectAgentLiveUpdateDto
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.ProjectStatusChanged,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 19, 5, 0, TimeSpan.Zero),
            ProjectStatus = new ProjectExecutionStatusChangedDto
            {
                Status = ProjectStatus.Completed,
            },
        });

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Analysis completed");
            StringAssert.Contains(cut.Markup, "Project execution finished successfully.");
        });
    }

    [TestMethod]
    public void LiveProjectStatusUpdate_TransitionsFromRunningToFailed()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000211");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000211");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000212");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 20, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 20, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Analysis running"));

        liveSubscriptionClient.Emit(new ProjectAgentLiveUpdateDto
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.ProjectStatusChanged,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 20, 5, 0, TimeSpan.Zero),
            ProjectStatus = new ProjectExecutionStatusChangedDto
            {
                Status = ProjectStatus.Failed,
            },
        });

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Analysis failed");
            StringAssert.Contains(cut.Markup, "Project execution ended with a failure.");
        });
    }

    [TestMethod]
    public void LiveProjectStatusUpdate_TransitionsFromRunningToCanceled()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000212");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000212");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000213");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 21, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 21, 1, 0, TimeSpan.Zero),
                            [])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Analysis running"));

        liveSubscriptionClient.Emit(new ProjectAgentLiveUpdateDto
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.ProjectStatusChanged,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 21, 5, 0, TimeSpan.Zero),
            ProjectStatus = new ProjectExecutionStatusChangedDto
            {
                Status = ProjectStatus.Canceled,
            },
        });

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Analysis canceled");
            StringAssert.Contains(cut.Markup, "Project execution was canceled.");
        });
    }

    [TestMethod]
    public void ReconnectRequired_ReloadsSnapshotAndResubscribesLive()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000206");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000206");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000207");

        ProjectAgentStatusSnapshotDto firstSnapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 15, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 15, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000208"),
                                    agentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "History v1")
                            ])
                    ])
            ]);

        ProjectAgentStatusSnapshotDto secondSnapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 15, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Completed,
                            new DateTimeOffset(2026, 5, 10, 15, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000208"),
                                    agentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "History v1"),
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000209"),
                                    agentId,
                                    2,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "History v2")
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([firstSnapshot, secondSnapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "History v1");
            Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls);
        });

        liveSubscriptionClient.TriggerReconnectRequired();

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(2, liveSubscriptionClient.SubscribeCalls);
            StringAssert.Contains(cut.Markup, "History v2");
            Assert.AreEqual("Completed", cut.Find(".agent-status-dot").GetAttribute("title"));
        });
    }

    [TestMethod]
    public void Reconnecting_ImmediatelyShowsReconnectingStateBeforeReload()
    {
        using Bunit.TestContext context = new();
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000207");
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000207");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000208");

        ProjectAgentStatusSnapshotDto snapshot = CreateSnapshot(
            projectId,
            [
                CreateGroup(
                    groupId,
                    "group-a",
                    "Group A",
                    new DateTimeOffset(2026, 5, 10, 16, 0, 0, TimeSpan.Zero),
                    [
                        CreateAgent(
                            agentId,
                            groupId,
                            "agent-a",
                            "Agent A",
                            ProjectAgentRunStatus.Running,
                            new DateTimeOffset(2026, 5, 10, 16, 1, 0, TimeSpan.Zero),
                            [
                                CreateTimelineEntry(
                                    Guid.Parse("73000000-0000-0000-0000-000000000210"),
                                    agentId,
                                    1,
                                    ProjectAgentTimelineEntryKind.Output,
                                    message: "History v1")
                            ])
                    ])
            ]);

        context.Services.AddSingleton(new HttpClient(new SnapshotMessageHandler([snapshot]))
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"http://localhost/agent-status?projectId={projectId}");

        IRenderedComponent<AgentStatusPage> cut = RenderAgentStatus(context);
        cut.WaitForAssertion(() => Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls));

        liveSubscriptionClient.TriggerReconnecting();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Live connection interrupted. Reconnecting...");
            StringAssert.Contains(cut.Markup, "Agent A");
            StringAssert.Contains(cut.Markup, "History v1");
        });
    }

    private static ProjectAgentStatusSnapshotDto CreateSnapshot(
        Guid projectId,
        IReadOnlyList<ProjectAgentGroupSnapshotDto> groups,
        ProjectStatus projectStatus = ProjectStatus.Reviewing) => new()
        {
            ProjectId = projectId,
            ProjectStatus = projectStatus,
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
        IReadOnlyList<ProjectAgentTimelineEntryDto> timeline,
        string systemPrompt = "") => new()
        {
            AgentId = agentId,
            GroupId = groupId,
            RuntimeKey = runtimeKey,
            DisplayName = displayName,
            SystemPrompt = systemPrompt,
            Status = status,
            CreatedAtUtc = createdAtUtc,
            HasLoadedHistory = timeline.Count > 0,
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
        IReadOnlyDictionary<Guid, ProjectAgentHistorySnapshotDto>? histories = null,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly Queue<ProjectAgentStatusSnapshotDto> _snapshots = new(snapshots);
        private readonly IReadOnlyDictionary<Guid, ProjectAgentHistorySnapshotDto> _histories = histories ?? new Dictionary<Guid, ProjectAgentHistorySnapshotDto>();
        private readonly HttpStatusCode _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_statusCode != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode));
            }

            string absolutePath = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (absolutePath.Contains("/history", StringComparison.Ordinal))
            {
                string[] segments = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                Guid agentId = Guid.Parse(segments[^2]);
                if (!_histories.TryGetValue(agentId, out ProjectAgentHistorySnapshotDto? history))
                    throw new InvalidOperationException($"No history response was configured for agent '{agentId}'.");

                return Task.FromResult(new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(JsonSerializer.Serialize(history)),
                });
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

    private static bool InvokeApplyLiveUpdate(IRenderedComponent<AgentStatusPage> cut, ProjectAgentLiveUpdateDto update)
    {
        MethodInfo? method = typeof(AgentStatusPage).GetMethod("ApplyLiveUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        bool changed = false;
        cut.InvokeAsync(() =>
        {
            changed = (bool)(method.Invoke(cut.Instance, [update]) ?? false);
            if (changed)
                cut.Render();
        }).GetAwaiter().GetResult();

        return changed;
    }

    private static ProjectAgentLiveUpdateDto CreateTimelineLiveUpdate(
        Guid projectId,
        Guid timelineEntryId,
        Guid agentId,
        long sequence,
        ProjectAgentTimelineEntryKind entryKind,
        DateTimeOffset occurredAtUtc,
        string? message = null,
        string? toolCallId = null,
        string? toolName = null,
        string? toolArguments = null,
        string? toolResult = null) => new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = occurredAtUtc,
            TimelineEntry = CreateTimelineEntry(
                timelineEntryId,
                agentId,
                sequence,
                entryKind,
                message,
                toolCallId,
                toolName,
                toolArguments,
                toolResult),
        };

    private static FakeProjectAgentStatusLiveSubscriptionClient RegisterLiveSubscriptionClient(Bunit.TestContext context)
    {
        FakeProjectAgentStatusLiveSubscriptionClient client = new();
        context.Services.AddSingleton<IProjectAgentStatusLiveSubscriptionClient>(client);
        return client;
    }

    private static IRenderedComponent<AgentStatusPage> RenderAgentStatus(Bunit.TestContext context)
    {
        context.Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
        return context.RenderComponent<AgentStatusPage>();
    }

    private sealed class FakeProjectAgentStatusLiveSubscriptionClient : IProjectAgentStatusLiveSubscriptionClient
    {
        private Func<ProjectAgentLiveUpdateDto, Task>? _onUpdate;
        private Func<Task>? _onReconnecting;
        private Func<Task>? _onReconnectRequired;

        public List<ProjectAgentLiveSubscriptionRequestDto> SubscribeCalls { get; } = [];

        public ProjectAgentLiveSubscriptionRequestDto? LastRequest => SubscribeCalls.LastOrDefault();

        public int UnsubscribeCallCount { get; private set; }

        public Exception? SubscribeException { get; set; }

        public Task SubscribeAsync(
            ProjectAgentLiveSubscriptionRequestDto request,
            Func<ProjectAgentLiveUpdateDto, Task> onUpdate,
            Func<Task> onReconnecting,
            Func<Task> onReconnectRequired,
            CancellationToken cancellationToken = default)
        {
            if (SubscribeException is not null)
                throw SubscribeException;

            SubscribeCalls.Add(request);
            _onUpdate = onUpdate;
            _onReconnecting = onReconnecting;
            _onReconnectRequired = onReconnectRequired;
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(CancellationToken cancellationToken = default)
        {
            UnsubscribeCallCount++;
            return Task.CompletedTask;
        }

        public void Emit(ProjectAgentLiveUpdateDto update)
        {
            Assert.IsNotNull(_onUpdate);
            _onUpdate(update).GetAwaiter().GetResult();
        }

        public void TriggerReconnectRequired()
        {
            Assert.IsNotNull(_onReconnectRequired);
            _onReconnectRequired().GetAwaiter().GetResult();
        }

        public void TriggerReconnecting()
        {
            Assert.IsNotNull(_onReconnecting);
            _onReconnecting().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
