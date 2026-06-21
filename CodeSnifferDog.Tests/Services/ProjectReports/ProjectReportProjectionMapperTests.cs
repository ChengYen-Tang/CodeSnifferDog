using CodeSnifferDog.Server.Services.ProjectReports.Projection;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class ProjectReportProjectionMapperTests
{
    [TestMethod]
    public void MapBundle_MapsProjectAndReportFieldsWithoutSorting()
    {
        ProjectReportProjectionMapper mapper = new();
        ProjectRuleReportProjection reportB = new(Guid.NewGuid(), "Rule B", "# B");
        ProjectRuleReportProjection reportA = new(Guid.NewGuid(), "Rule A", "# A");

        var dto = mapper.MapBundle(new ProjectReportProjectProjection("repo.zip", [reportB, reportA]));

        Assert.AreEqual("repo.zip", dto.OriginalFileName);
        Assert.AreEqual(reportB.ReportId, dto.Reports[0].ReportId);
        Assert.AreEqual("Rule B", dto.Reports[0].RuleName);
        Assert.AreEqual("# B", dto.Reports[0].MarkdownContent);
        Assert.AreEqual(reportA.ReportId, dto.Reports[1].ReportId);
    }

    [TestMethod]
    public void MapList_MapsProjectAndReportFieldsWithoutSorting()
    {
        ProjectReportProjectionMapper mapper = new();
        ProjectRuleReportProjection report = new(Guid.NewGuid(), "Rule A", "# A");

        var dto = mapper.MapList(new ProjectReportProjectProjection("repo.zip", [report]));

        Assert.AreEqual("repo.zip", dto.OriginalFileName);
        Assert.AreEqual(report.ReportId, dto.Reports[0].ReportId);
        Assert.AreEqual("Rule A", dto.Reports[0].RuleName);
    }

    [TestMethod]
    public void MapContent_MapsReportFields()
    {
        ProjectReportProjectionMapper mapper = new();
        ProjectRuleReportProjection report = new(Guid.NewGuid(), "Rule A", "# A");

        var dto = mapper.MapContent(report);

        Assert.AreEqual(report.ReportId, dto.ReportId);
        Assert.AreEqual("Rule A", dto.RuleName);
        Assert.AreEqual("# A", dto.MarkdownContent);
    }
}
