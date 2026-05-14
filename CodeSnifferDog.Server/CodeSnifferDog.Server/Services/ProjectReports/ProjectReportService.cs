using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Server.Services.ProjectReports;

public sealed class ProjectReportService(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory) : IProjectReportService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

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

        ProjectRecord? project = await dbContext.Projects
            .AsNoTracking()
            .Include(project => project.RuleReports.OrderBy(report => report.RuleName))
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return null;

        return new ProjectReportBundleDto
        {
            OriginalFileName = project.OriginalFileName,
            Reports = project.RuleReports
                .OrderBy(report => report.RuleName, StringComparer.OrdinalIgnoreCase)
                .Select(MapReport)
                .ToList(),
        };
    }

    public async Task<ProjectReportListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .AsNoTracking()
            .Include(project => project.RuleReports.OrderBy(report => report.RuleName))
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return null;

        return new ProjectReportListDto
        {
            OriginalFileName = project.OriginalFileName,
            Reports = project.RuleReports
                .OrderBy(report => report.RuleName, StringComparer.OrdinalIgnoreCase)
                .Select(MapReportListItem)
                .ToList(),
        };
    }

    public async Task<ProjectReportContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRuleReportRecord? report = await dbContext.ProjectRuleReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.Id == reportId,
                cancellationToken);

        return report is null ? null : MapReportContent(report);
    }

    private static ProjectRuleReportDto MapReport(ProjectRuleReportRecord report) => new()
    {
        ReportId = report.Id,
        RuleName = report.RuleName,
        MarkdownContent = report.MarkdownContent,
    };

    private static ProjectReportListItemDto MapReportListItem(ProjectRuleReportRecord report) => new()
    {
        ReportId = report.Id,
        RuleName = report.RuleName,
    };

    private static ProjectReportContentDto MapReportContent(ProjectRuleReportRecord report) => new()
    {
        ReportId = report.Id,
        RuleName = report.RuleName,
        MarkdownContent = report.MarkdownContent,
    };

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
