using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectReports.Queries;

/// <summary>
/// Loads persisted project report rows and shapes them into report projections.
/// </summary>
/// <param name="dbContextFactory">Factory used to create database contexts for report queries.</param>
internal sealed class QueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory) : IQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>
    /// Creates the query that joins a project row with its optional stored rule reports.
    /// </summary>
    /// <param name="dbContext">Database context used to compose the query.</param>
    /// <param name="projectId">Project identifier to query.</param>
    /// <returns>The joined query rows used to build project report projections.</returns>
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

    /// <summary>
    /// Joined row used while projecting a project and its optional report row.
    /// </summary>
    /// <param name="OriginalFileName">Original uploaded file name.</param>
    /// <param name="ReportId">Report identifier, when one exists.</param>
    /// <param name="RuleName">Rule name, when one exists.</param>
    /// <param name="MarkdownContent">Markdown content, when one exists.</param>
    internal sealed record QueryRow(
        string OriginalFileName,
        Guid? ReportId,
        string? RuleName,
        string? MarkdownContent);
}
