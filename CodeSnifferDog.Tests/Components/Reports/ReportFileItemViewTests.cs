using AngleSharp.Dom;
using Bunit;
using CodeSnifferDog.Server.Client.Components.Reports;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeSnifferDog.Tests.Components.Reports;

[TestClass]
public sealed class ReportFileItemViewTests
{
    [TestMethod]
    public void RendersReportMetadataDownloadLinkAndSelectedState()
    {
        using Bunit.TestContext context = new();
        Guid projectId = Guid.Parse("80000000-0000-0000-0000-000000000201");
        ListItemDto report = new()
        {
            ReportId = Guid.Parse("81000000-0000-0000-0000-000000000201"),
            RuleName = "rule-file",
        };

        IRenderedComponent<ReportFileItemView> cut = context.RenderComponent<ReportFileItemView>(
            parameters => parameters
                .Add(component => component.ProjectId, projectId)
                .Add(component => component.Report, report)
                .Add(component => component.IsSelected, true));

        IElement item = cut.Find(".report-file-item");
        StringAssert.Contains(item.ClassName, "active");
        StringAssert.Contains(cut.Markup, "rule-file.md");
        StringAssert.Contains(cut.Markup, "rule-file");

        IElement download = cut.Find(".report-file-download");
        Assert.AreEqual($"/api/projects/{projectId}/reports/{report.ReportId}/download", download.GetAttribute("href"));
        Assert.AreEqual("rule-file.md", download.GetAttribute("download"));
    }

    [TestMethod]
    public void SelectButtonInvokesCallbackWithReport()
    {
        using Bunit.TestContext context = new();
        ListItemDto report = new()
        {
            ReportId = Guid.Parse("81000000-0000-0000-0000-000000000202"),
            RuleName = "rule-callback",
        };
        Guid? selectedReportId = null;

        IRenderedComponent<ReportFileItemView> cut = context.RenderComponent<ReportFileItemView>(
            parameters => parameters
                .Add(component => component.ProjectId, Guid.Parse("80000000-0000-0000-0000-000000000202"))
                .Add(component => component.Report, report)
                .Add(component => component.OnSelectReport, EventCallback.Factory.Create<ListItemDto>(
                    this,
                    selectedReport => selectedReportId = selectedReport.ReportId)));

        cut.Find(".report-file-select").Click();

        Assert.AreEqual(report.ReportId, selectedReportId);
    }
}
