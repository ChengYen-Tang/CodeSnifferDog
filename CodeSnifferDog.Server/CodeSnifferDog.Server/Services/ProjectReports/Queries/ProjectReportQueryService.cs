using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectReports.Queries;

internal sealed class ProjectReportQueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory) : IProjectReportQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<ProjectReportProjectProjection?> GetProjectReportsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .AsNoTracking()
            .Include(project => project.RuleReports)
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return null;

        return new ProjectReportProjectProjection(
            project.OriginalFileName,
            project.RuleReports
                .OrderBy(report => report.RuleName, StringComparer.OrdinalIgnoreCase)
                .Select(MapReport)
                .ToList());
    }

    public async Task<ProjectRuleReportProjection?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRuleReportRecord? report = await dbContext.ProjectRuleReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                report => report.ProjectId == projectId && report.Id == reportId,
                cancellationToken);

        return report is null ? null : MapReport(report);
    }

    private static ProjectRuleReportProjection MapReport(ProjectRuleReportRecord report) =>
        new(report.Id, report.RuleName, report.MarkdownContent);
}
