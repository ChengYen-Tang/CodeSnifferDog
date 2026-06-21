using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Server.Services.ProjectReports;

internal sealed class ProjectReportService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectReportProjectionMapper projectionMapper) : IProjectReportService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectReportProjectionMapper _projectionMapper = projectionMapper;

    public async Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<ProjectRuleReportDraft> reports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reports);

        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .Include(project => project.RuleReports)
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            throw new InvalidOperationException($"Project was not found: {projectId}");

        dbContext.ProjectRuleReports.RemoveRange(project.RuleReports);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        foreach (ProjectRuleReportDraft report in reports)
        {
            string ruleKey = ValidateRequiredText(report.RuleKey, nameof(ProjectRuleReportDraft.RuleKey));
            string ruleName = ValidateRequiredText(report.RuleName, nameof(ProjectRuleReportDraft.RuleName));
            string markdownContent = ValidateRequiredText(report.MarkdownContent, nameof(ProjectRuleReportDraft.MarkdownContent));

            ProjectRuleReportRecord reportRecord = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RuleKey = ruleKey,
                RuleKeyHash = ComputeStableHash(ruleKey),
                RuleName = ruleName,
                MarkdownContent = markdownContent,
                CreatedAtUtc = nowUtc,
            };

            dbContext.ProjectRuleReports.Add(reportRecord);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectReportBundleDto?> GetProjectReportBundleAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        string? originalFileName = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OriginalFileName)
            .SingleOrDefaultAsync(cancellationToken);

        if (originalFileName is null)
            return null;

        ProjectReportProjectProjection project = new(
            originalFileName,
            await LoadReportProjectionsAsync(dbContext, projectId, cancellationToken));

        return _projectionMapper.MapBundle(SortReports(project));
    }

    public async Task<ProjectReportListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        string? originalFileName = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OriginalFileName)
            .SingleOrDefaultAsync(cancellationToken);

        if (originalFileName is null)
            return null;

        ProjectReportProjectProjection project = new(
            originalFileName,
            await LoadReportProjectionsAsync(dbContext, projectId, cancellationToken));

        return _projectionMapper.MapList(SortReports(project));
    }

    public async Task<ProjectReportContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRuleReportProjection? report = await dbContext.ProjectRuleReports
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Id == reportId)
            .Select(item => new ProjectRuleReportProjection(
                item.Id,
                item.RuleName,
                item.MarkdownContent))
            .SingleOrDefaultAsync(cancellationToken);

        return report is null ? null : _projectionMapper.MapContent(report);
    }

    private static ProjectReportProjectProjection SortReports(ProjectReportProjectProjection project) =>
        project with
        {
            Reports = project.Reports
                .OrderBy(report => report.RuleName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

    private static async Task<List<ProjectRuleReportProjection>> LoadReportProjectionsAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectRuleReports
            .AsNoTracking()
            .Where(report => report.ProjectId == projectId)
            .Select(report => new ProjectRuleReportProjection(
                report.Id,
                report.RuleName,
                report.MarkdownContent))
            .ToListAsync(cancellationToken);

    private static string ValidateRequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} cannot be null, empty, or whitespace.", parameterName);

        return value;
    }

    private static string ComputeStableHash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
