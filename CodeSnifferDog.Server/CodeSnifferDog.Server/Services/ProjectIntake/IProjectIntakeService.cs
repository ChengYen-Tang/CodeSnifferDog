namespace CodeSnifferDog.Server.Services.ProjectIntake;

public interface IProjectIntakeService
{
    Task<ProjectUploadResult> UploadAsync(IFormFile zipFile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ProjectSummaryDto?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
