using Bunit;
using AngleSharp.Dom;
using CodeSnifferDog.Server.Components.Pages;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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
        FakeProjectAgentStatusLiveSubscriptionClient liveSubscriptionClient = RegisterLiveSubscriptionClient(context);
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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();
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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();
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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();
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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();
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
    public void SnapshotLoad_SubscribesToLiveUpdatesWithPerAgentCursors()
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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, liveSubscriptionClient.SubscribeCalls);
            Assert.IsNotNull(liveSubscriptionClient.LastRequest);
            Assert.AreEqual(projectId, liveSubscriptionClient.LastRequest.ProjectId);
            Assert.AreEqual(snapshotGeneratedAtUtc, liveSubscriptionClient.LastRequest.SnapshotGeneratedAtUtc);
            Assert.HasCount(2, liveSubscriptionClient.LastRequest.AgentCursors);
        });

        Dictionary<Guid, long> cursors = liveSubscriptionClient.LastRequest!.AgentCursors.ToDictionary(
            cursor => cursor.AgentId,
            cursor => cursor.LatestSequence);

        Assert.AreEqual(3L, cursors[agentAId]);
        Assert.AreEqual(0L, cursors[agentBId]);

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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Agent A");
            StringAssert.Contains(cut.Markup, "Existing history");
            StringAssert.Contains(cut.Markup, "Failed to connect live updates: SignalR handshake failed");
            Assert.DoesNotContain(cut.Markup, "Failed to load snapshot:");
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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();

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

        IRenderedComponent<AgentStatus> cut = context.RenderComponent<AgentStatus>();
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

    private static void InvokeApplyLiveUpdate(IRenderedComponent<AgentStatus> cut, ProjectAgentLiveUpdateDto update)
    {
        MethodInfo? method = typeof(AgentStatus).GetMethod("ApplyLiveUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        cut.InvokeAsync(() =>
        {
            method.Invoke(cut.Instance, [update]);
            cut.Render();
        }).GetAwaiter().GetResult();
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
