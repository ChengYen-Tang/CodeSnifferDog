using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports;

public interface IProjectReportService
{
    Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<ProjectRuleReportDraft> reports,
        CancellationToken cancellationToken = default);

    Task<ProjectReportListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectReportBundleDto?> GetProjectReportBundleAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectReportContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
