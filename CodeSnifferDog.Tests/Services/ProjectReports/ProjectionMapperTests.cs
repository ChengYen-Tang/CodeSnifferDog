using CodeSnifferDog.Server.Services.ProjectReports.Projection;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class ProjectionMapperTests
{
    [TestMethod]
    public void MapBundle_MapsProjectAndReportFieldsWithoutSorting()
    {
        ProjectionMapper mapper = new();
        RuleReportProjection reportB = new(Guid.CreateVersion7(), "Rule B", "# B");
        RuleReportProjection reportA = new(Guid.CreateVersion7(), "Rule A", "# A");

        var dto = mapper.MapBundle(new ProjectProjection("repo.zip", [reportB, reportA]));

        Assert.AreEqual("repo.zip", dto.OriginalFileName);
        Assert.AreEqual(reportB.ReportId, dto.Reports[0].ReportId);
        Assert.AreEqual("Rule B", dto.Reports[0].RuleName);
        Assert.AreEqual("# B", dto.Reports[0].MarkdownContent);
        Assert.AreEqual(reportA.ReportId, dto.Reports[1].ReportId);
    }

    [TestMethod]
    public void MapList_MapsProjectAndReportFieldsWithoutSorting()
    {
        ProjectionMapper mapper = new();
        RuleReportProjection report = new(Guid.CreateVersion7(), "Rule A", "# A");

        var dto = mapper.MapList(new ProjectProjection("repo.zip", [report]));

        Assert.AreEqual("repo.zip", dto.OriginalFileName);
        Assert.AreEqual(report.ReportId, dto.Reports[0].ReportId);
        Assert.AreEqual("Rule A", dto.Reports[0].RuleName);
    }

    [TestMethod]
    public void MapContent_MapsReportFields()
    {
        ProjectionMapper mapper = new();
        RuleReportProjection report = new(Guid.CreateVersion7(), "Rule A", "# A");

        var dto = mapper.MapContent(report);

        Assert.AreEqual(report.ReportId, dto.ReportId);
        Assert.AreEqual("Rule A", dto.RuleName);
        Assert.AreEqual("# A", dto.MarkdownContent);
    }
}
