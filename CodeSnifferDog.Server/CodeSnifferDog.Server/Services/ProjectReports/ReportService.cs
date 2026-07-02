using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Server.Services.ProjectReports;

internal sealed class ReportService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IQueryService queryService,
    IProjectionMapper projectionMapper) : IReportService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IQueryService _queryService = queryService;
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    public async Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<RuleReportDraft> reports,
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
        foreach (RuleReportDraft report in reports)
        {
            string ruleKey = ValidateRequiredText(report.RuleKey, nameof(RuleReportDraft.RuleKey));
            string ruleName = ValidateRequiredText(report.RuleName, nameof(RuleReportDraft.RuleName));
            string markdownContent = ValidateRequiredText(report.MarkdownContent, nameof(RuleReportDraft.MarkdownContent));

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
        ProjectProjection? project = await _queryService.GetProjectReportsAsync(projectId, cancellationToken);
        return project is null ? null : _projectionMapper.MapBundle(project);
    }

    public async Task<ProjectReportListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ProjectProjection? project = await _queryService.GetProjectReportsAsync(projectId, cancellationToken);
        return project is null ? null : _projectionMapper.MapList(project);
    }

    public async Task<ProjectReportContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        RuleReportProjection? report = await _queryService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        return report is null ? null : _projectionMapper.MapContent(report);
    }

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
