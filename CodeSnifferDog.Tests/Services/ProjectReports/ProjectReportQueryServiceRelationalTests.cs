using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Tests.Services.ProjectReports;

[TestClass]
public sealed class ProjectReportQueryServiceRelationalTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void CreateProjectReportsQuery_LeftJoinProjectionTranslatesOnRelationalProvider()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=CodeSnifferDogTranslationTest;Trusted_Connection=True;")
            .Options;
        using CodeSnifferDogServerDbContext dbContext = new(options);

        string sql = ProjectReportQueryService
            .CreateProjectReportsQuery(dbContext, Guid.NewGuid())
            .ToQueryString();

        StringAssert.Contains(sql, "LEFT JOIN");
        StringAssert.Contains(sql, "ProjectRuleReports");
    }
}
