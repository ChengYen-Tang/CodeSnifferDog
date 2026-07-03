using Bunit;
using CodeSnifferDog.Server.Client.Components.Reports;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeSnifferDog.Tests.Components.Reports;

[TestClass]
public sealed class ReportsSidebarPaneTests
{
    [TestMethod]
    public void RendersLoadingErrorAndEmptyStates()
    {
        using Bunit.TestContext context = new();

        IRenderedComponent<ReportsSidebarPane> loading = context.RenderComponent<ReportsSidebarPane>(
            parameters => parameters.Add(component => component.IsListLoading, true));
        StringAssert.Contains(loading.Markup, "Loading reports...");

        IRenderedComponent<ReportsSidebarPane> error = context.RenderComponent<ReportsSidebarPane>(
            parameters => parameters.Add(component => component.ListErrorMessage, "Failed to load reports: 500"));
        StringAssert.Contains(error.Markup, "Failed to load reports: 500");

        IRenderedComponent<ReportsSidebarPane> empty = context.RenderComponent<ReportsSidebarPane>(
            parameters => parameters.Add(component => component.ReportList, new ListDto
            {
                OriginalFileName = "empty.zip",
                Reports = [],
            }));
        StringAssert.Contains(empty.Markup, "No reports available for this project.");
    }

    [TestMethod]
    public void RendersReportsZipLinkAndSelectedState()
    {
        using Bunit.TestContext context = new();
        Guid projectId = Guid.Parse("80000000-0000-0000-0000-000000000401");
        Guid selectedReportId = Guid.Parse("81000000-0000-0000-0000-000000000402");
        ListDto reportList = new()
        {
            OriginalFileName = "demo.zip",
            Reports =
            [
                new ListItemDto
                {
                    ReportId = Guid.Parse("81000000-0000-0000-0000-000000000401"),
                    RuleName = "rule-a",
                },
                new ListItemDto
                {
                    ReportId = selectedReportId,
                    RuleName = "rule-b",
                },
            ],
        };

        IRenderedComponent<ReportsSidebarPane> cut = context.RenderComponent<ReportsSidebarPane>(
            parameters => parameters
                .Add(component => component.ProjectId, projectId)
                .Add(component => component.ReportList, reportList)
                .Add(component => component.SelectedReportId, selectedReportId));

        StringAssert.Contains(cut.Markup, "demo.zip");
        StringAssert.Contains(cut.Markup, "2 markdown reports available");
        Assert.AreEqual($"/api/projects/{projectId}/reports/download", cut.Find(".project-action-button").GetAttribute("href"));
        Assert.AreEqual(2, cut.FindAll(".report-file-item").Count);
        StringAssert.Contains(cut.Find(".report-file-item.active").TextContent, "rule-b");
    }

    [TestMethod]
    public void SelectReportInvokesCallbackAndLargeListRendersAllItems()
    {
        using Bunit.TestContext context = new();
        Guid projectId = Guid.Parse("80000000-0000-0000-0000-000000000403");
        ListDto reportList = new()
        {
            OriginalFileName = "large-demo.zip",
            Reports = Enumerable.Range(1, 200)
                .Select(index => new ListItemDto
                {
                    ReportId = Guid.Parse($"81000000-0000-0000-0002-{index:000000000000}"),
                    RuleName = $"rule-{index:000}",
                })
                .ToList(),
        };
        Guid? selectedReportId = null;

        IRenderedComponent<ReportsSidebarPane> cut = context.RenderComponent<ReportsSidebarPane>(
            parameters => parameters
                .Add(component => component.ProjectId, projectId)
                .Add(component => component.ReportList, reportList)
                .Add(component => component.OnSelectReport, EventCallback.Factory.Create<ListItemDto>(
                    this,
                    report => selectedReportId = report.ReportId)));

        Assert.AreEqual(200, cut.FindAll(".report-file-item").Count);
        StringAssert.Contains(cut.Markup, "rule-200.md");

        cut.FindAll(".report-file-select")[49].Click();

        Assert.AreEqual(reportList.Reports[49].ReportId, selectedReportId);
    }
}
