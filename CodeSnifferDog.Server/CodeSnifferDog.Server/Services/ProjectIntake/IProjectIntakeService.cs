using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

public interface IProjectIntakeService
{
    Task<ProjectUploadResult> UploadAsync(IFormFile zipFile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectListItemDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ProjectSummaryDto?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}
