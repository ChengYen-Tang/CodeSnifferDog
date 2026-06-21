using CodeSnifferDog.Server.Data;
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

        string? originalFileName = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OriginalFileName)
            .SingleOrDefaultAsync(cancellationToken);

        if (originalFileName is null)
            return null;

        List<ProjectRuleReportProjection> reports = await dbContext.ProjectRuleReports
            .AsNoTracking()
            .Where(report => report.ProjectId == projectId)
            .Select(report => new ProjectRuleReportProjection(
                report.Id,
                report.RuleName,
                report.MarkdownContent))
            .ToListAsync(cancellationToken);

        return new ProjectReportProjectProjection(
            originalFileName,
            reports
                .OrderBy(report => report.RuleName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public async Task<ProjectRuleReportProjection?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.ProjectRuleReports
            .AsNoTracking()
            .Where(report => report.ProjectId == projectId && report.Id == reportId)
            .Select(report => new ProjectRuleReportProjection(
                report.Id,
                report.RuleName,
                report.MarkdownContent))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
