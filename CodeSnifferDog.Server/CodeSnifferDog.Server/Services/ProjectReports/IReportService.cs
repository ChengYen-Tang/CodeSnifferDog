using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports;

public interface IReportService
{
    Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<RuleReportDraft> reports,
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
