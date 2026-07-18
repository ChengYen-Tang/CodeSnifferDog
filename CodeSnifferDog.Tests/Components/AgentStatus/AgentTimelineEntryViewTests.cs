using Bunit;
using CodeSnifferDog.Server.Client.Components.AgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.AspNetCore.Components;

namespace CodeSnifferDog.Tests.Components.AgentStatus;

[TestClass]
public sealed class AgentTimelineEntryViewTests
{
    [TestMethod]
    public void RendersInputAndOutputMessages()
    {
        using Bunit.TestContext context = new();

        IRenderedComponent<AgentTimelineEntryView> input = RenderEntry(
            context,
            CreateEntry(TimelineEntryKind.Input, "User request"));
        IRenderedComponent<AgentTimelineEntryView> output = RenderEntry(
            context,
            CreateEntry(TimelineEntryKind.Output, "Agent response"));

        StringAssert.Contains(input.Find(".agent-message").ClassName, "user-message");
        StringAssert.Contains(input.Markup, "User request");
        StringAssert.Contains(output.Find(".agent-message").ClassName, "agent-message-left");
        StringAssert.Contains(output.Markup, "Agent response");
    }

    [TestMethod]
    public void RendersToolSummaryAndExpandedDetails()
    {
        using Bunit.TestContext context = new();
        TimelineEntryDto entry = CreateEntry(
            TimelineEntryKind.Tool,
            message: null,
            toolCallId: "tool-call-1",
            toolName: "Shell",
            toolArguments: "{ \"command\": \"dotnet test\" }",
            toolResult: "Passed");

        IRenderedComponent<AgentTimelineEntryView> cut = RenderEntry(context, entry, isExpanded: true);

        StringAssert.Contains(cut.Find(".tool-call-summary").TextContent, "Shell");
        StringAssert.Contains(cut.Find(".tool-call-item").TextContent, "dotnet test");
        StringAssert.Contains(cut.Find(".tool-call-item").TextContent, "Passed");
    }

    [TestMethod]
    public void ClickingToolSummaryRaisesToggleCallback()
    {
        using Bunit.TestContext context = new();
        string? toggledKey = null;
        TimelineEntryDto entry = CreateEntry(
            TimelineEntryKind.Tool,
            message: null,
            toolCallId: "tool-call-2",
            toolName: "Ripgrep");

        IRenderedComponent<AgentTimelineEntryView> cut = context.RenderComponent<AgentTimelineEntryView>(
            parameters => parameters
                .Add(component => component.Entry, entry)
                .Add(component => component.OnToggleToolDetails, EventCallback.Factory.Create<string>(this, value => toggledKey = value)));

        cut.Find(".tool-call-summary").Click();

        Assert.AreEqual("tool-call-2", toggledKey);
    }

    [TestMethod]
    public void RendersCompactionNotice()
    {
        using Bunit.TestContext context = new();

        IRenderedComponent<AgentTimelineEntryView> cut = RenderEntry(
            context,
            CreateEntry(TimelineEntryKind.Compaction, message: null));

        Assert.AreEqual(1, cut.FindAll(".agent-compaction-notice").Count);
        StringAssert.Contains(cut.Markup, "Context compacted");
        Assert.IsEmpty(cut.FindAll(".agent-message"));
    }

    private static IRenderedComponent<AgentTimelineEntryView> RenderEntry(
        Bunit.TestContext context,
        TimelineEntryDto entry,
        bool isExpanded = false) =>
        context.RenderComponent<AgentTimelineEntryView>(
            parameters => parameters
                .Add(component => component.Entry, entry)
                .Add(component => component.IsExpanded, isExpanded));

    private static TimelineEntryDto CreateEntry(
        TimelineEntryKind kind,
        string? message,
        string? toolCallId = null,
        string? toolName = null,
        string? toolArguments = null,
        string? toolResult = null) => new()
        {
            TimelineEntryId = Guid.NewGuid(),
            AgentId = Guid.Parse("92000000-0000-0000-0000-000000000001"),
            Sequence = 1,
            EntryKind = kind,
            OccurredAtUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            Message = message,
            ToolCallId = toolCallId,
            ToolName = toolName,
            ToolArguments = toolArguments,
            ToolResult = toolResult,
        };
}
