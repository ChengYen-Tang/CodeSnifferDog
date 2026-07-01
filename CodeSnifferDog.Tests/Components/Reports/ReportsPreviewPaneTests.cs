using Bunit;
using CodeSnifferDog.Server.Client.Components.Reports;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeSnifferDog.Tests.Components.Reports;

[TestClass]
public sealed class ReportsPreviewPaneTests
{
    [TestMethod]
    public void RendersLoadingStateWithSelectedReportMetadata()
    {
        using Bunit.TestContext context = new();
        ProjectReportListItemDto selectedReport = new()
        {
            ReportId = Guid.Parse("81000000-0000-0000-0000-000000000301"),
            RuleName = "rule-loading",
        };

        IRenderedComponent<ReportsPreviewPane> cut = context.RenderComponent<ReportsPreviewPane>(
            parameters => parameters
                .Add(component => component.IsContentLoading, true)
                .Add(component => component.SelectedReportListItem, selectedReport));

        StringAssert.Contains(cut.Markup, "rule-loading.md");
        StringAssert.Contains(cut.Markup, "Loading report...");
    }

    [TestMethod]
    public void RendersContentMarkupAndToolbar()
    {
        using Bunit.TestContext context = new();
        ProjectReportContentDto content = new()
        {
            ReportId = Guid.Parse("81000000-0000-0000-0000-000000000302"),
            RuleName = "rule-content",
            MarkdownContent = "# ignored by pane",
        };

        IRenderedComponent<ReportsPreviewPane> cut = context.RenderComponent<ReportsPreviewPane>(
            parameters => parameters
                .Add(component => component.SelectedReportContent, content)
                .Add(component => component.SelectedReportMarkup, new MarkupString("<h1>Rendered markdown</h1>")));

        StringAssert.Contains(cut.Markup, "rule-content.md");
        StringAssert.Contains(cut.Markup, "<h1>Rendered markdown</h1>");
    }

    [TestMethod]
    public void RendersErrorAndNoSelectionStates()
    {
        using Bunit.TestContext context = new();

        IRenderedComponent<ReportsPreviewPane> error = context.RenderComponent<ReportsPreviewPane>(
            parameters => parameters.Add(component => component.ContentErrorMessage, "Failed to load report content: 500"));

        StringAssert.Contains(error.Markup, "Failed to load report content: 500");
        StringAssert.Contains(error.Find(".markdown-body").ClassName, "text-danger");

        IRenderedComponent<ReportsPreviewPane> empty = context.RenderComponent<ReportsPreviewPane>(
            parameters => parameters
                .Add(component => component.IsListLoading, false)
                .Add(component => component.ListErrorMessage, (string?)null));

        StringAssert.Contains(empty.Markup, "No report selected.");
    }
}
