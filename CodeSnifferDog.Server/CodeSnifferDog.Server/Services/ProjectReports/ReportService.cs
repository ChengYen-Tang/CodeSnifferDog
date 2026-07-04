using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CodeSnifferDog.Server.Services.ProjectReports;

/// <summary>
/// Persists generated rule reports and projects them into shared DTOs for API consumers.
/// </summary>
/// <param name="dbContextFactory">Factory used to create database contexts for report persistence.</param>
/// <param name="queryService">Query service that loads persisted report projections.</param>
/// <param name="projectionMapper">Mapper that converts persisted projections to shared DTOs.</param>
internal sealed class ReportService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IQueryService queryService,
    IProjectionMapper projectionMapper) : IReportService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IQueryService _queryService = queryService;
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="reports" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The target project does not exist.</exception>
    /// <exception cref="ArgumentException">One of the supplied drafts contains required text that is null, empty, or whitespace.</exception>
    public async Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<RuleDraft> reports,
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
        foreach (RuleDraft report in reports)
        {
            string ruleKey = ValidateRequiredText(report.RuleKey, nameof(RuleDraft.RuleKey));
            string ruleName = ValidateRequiredText(report.RuleName, nameof(RuleDraft.RuleName));
            string markdownContent = ValidateRequiredText(report.MarkdownContent, nameof(RuleDraft.MarkdownContent));

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

    /// <inheritdoc />
    public async Task<BundleDto?> GetProjectReportBundleAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        ProjectProjection? project = await _queryService.GetProjectReportsAsync(projectId, cancellationToken);
        return project is null ? null : _projectionMapper.MapBundle(project);
    }

    /// <inheritdoc />
    public async Task<ListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ProjectProjection? project = await _queryService.GetProjectReportsAsync(projectId, cancellationToken);
        return project is null ? null : _projectionMapper.MapList(project);
    }

    /// <inheritdoc />
    public async Task<ContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        RuleReportProjection? report = await _queryService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        return report is null ? null : _projectionMapper.MapContent(report);
    }

    /// <summary>
    /// Validates required draft text before persistence.
    /// </summary>
    /// <param name="value">Candidate text.</param>
    /// <param name="parameterName">Name used in the thrown exception when validation fails.</param>
    /// <returns>The validated text.</returns>
    /// <exception cref="ArgumentException"><paramref name="value" /> is null, empty, or whitespace.</exception>
    private static string ValidateRequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} cannot be null, empty, or whitespace.", parameterName);

        return value;
    }

    /// <summary>
    /// Computes a stable SHA-256 hash for one rule key.
    /// </summary>
    /// <param name="value">Rule key to hash.</param>
    /// <returns>The uppercase hexadecimal SHA-256 hash.</returns>
    private static string ComputeStableHash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
