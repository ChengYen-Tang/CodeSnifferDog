using CodeSnifferDog.Server.Shared.Reports;

namespace CodeSnifferDog.Server.Services.ProjectReports;

public interface IReportService
{
    Task ReplaceProjectReportsAsync(
        Guid projectId,
        IReadOnlyList<RuleDraft> reports,
        CancellationToken cancellationToken = default);

    Task<ListDto?> GetProjectReportListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<BundleDto?> GetProjectReportBundleAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ContentDto?> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
