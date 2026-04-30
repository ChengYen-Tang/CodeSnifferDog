using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectReports;

public sealed class ProjectReportService(CodeSnifferDogServerDbContext dbContext) : IProjectReportService
{
    private readonly CodeSnifferDogServerDbContext _dbContext = dbContext;

    public async Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<ProjectRuleReportDraft> reports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reports);

        ProjectRecord? project = await _dbContext.Projects
            .Include(project => project.RuleReports)
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            throw new InvalidOperationException($"Project was not found: {projectId}");

        _dbContext.ProjectRuleReports.RemoveRange(project.RuleReports);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        foreach (ProjectRuleReportDraft report in reports)
        {
            string ruleName = ValidateRequiredText(report.RuleName, nameof(ProjectRuleReportDraft.RuleName));
            string markdownContent = ValidateRequiredText(report.MarkdownContent, nameof(ProjectRuleReportDraft.MarkdownContent));

            ProjectRuleReportRecord reportRecord = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RuleName = ruleName,
                MarkdownContent = markdownContent,
                CreatedAtUtc = nowUtc,
            };

            _dbContext.ProjectRuleReports.Add(reportRecord);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectReportBundleDto?> GetProjectReportBundleAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        ProjectRecord? project = await _dbContext.Projects
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

    public async Task<ProjectRuleReportDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        ProjectRuleReportRecord? report = await _dbContext.ProjectRuleReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.Id == reportId,
                cancellationToken);

        return report is null ? null : MapReport(report);
    }

    private static ProjectRuleReportDto MapReport(ProjectRuleReportRecord report) => new()
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
}
