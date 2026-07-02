using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectReports.Queries;

internal sealed class QueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory) : IQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<ProjectProjection?> GetProjectReportsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<QueryRow> rows = await CreateProjectReportsQuery(dbContext, projectId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return null;

        return new ProjectProjection(
            rows[0].OriginalFileName,
            rows
                .Where(row => row.ReportId.HasValue)
                .Select(row => new RuleReportProjection(
                    row.ReportId!.Value,
                    row.RuleName!,
                    row.MarkdownContent!))
                .OrderBy(report => report.RuleName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public async Task<RuleReportProjection?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.ProjectRuleReports
            .AsNoTracking()
            .Where(report => report.ProjectId == projectId && report.Id == reportId)
            .Select(report => new RuleReportProjection(
                report.Id,
                report.RuleName,
                report.MarkdownContent))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal static IQueryable<QueryRow> CreateProjectReportsQuery(
        CodeSnifferDogServerDbContext dbContext,
        Guid projectId) =>
        from project in dbContext.Projects.AsNoTracking()
        where project.Id == projectId
        join report in dbContext.ProjectRuleReports.AsNoTracking()
            on project.Id equals report.ProjectId into projectReports
        from report in projectReports.DefaultIfEmpty()
        select new QueryRow(
            project.OriginalFileName,
            report == null ? null : (Guid?)report.Id,
            report == null ? null : report.RuleName,
            report == null ? null : report.MarkdownContent);

    internal sealed record QueryRow(
        string OriginalFileName,
        Guid? ReportId,
        string? RuleName,
        string? MarkdownContent);
}
