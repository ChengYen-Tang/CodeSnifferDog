using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;

/// <summary>
/// Requeues interrupted projects or marks them failed when their artifacts can no longer be recovered.
/// </summary>
internal sealed class Service(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IExecutionArtifactStore artifactStore) : IService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IExecutionArtifactStore _artifactStore = artifactStore;

    /// <inheritdoc />
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProjectRecord> interruptedProjects = await dbContext.Projects
            .Where(project => project.Status == ProjectProcessingStatus.Reviewing)
            .ToListAsync(cancellationToken);

        if (interruptedProjects.Count == 0)
            return;

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        foreach (ProjectRecord project in interruptedProjects)
        {
            if (!_artifactStore.StoredZipExists(project.StoredZipRelativePath)
                && !_artifactStore.ExtractedProjectExists(project.Id))
            {
                project.Status = ProjectProcessingStatus.Failed;
                project.UpdatedAtUtc = nowUtc;
                project.FinishedAtUtc = nowUtc;
                project.FailureReason = "Project artifacts were lost before recovery could restart analysis.";
                continue;
            }

            project.Status = ProjectProcessingStatus.Queued;
            project.UpdatedAtUtc = nowUtc;
            project.QueueTimestampUtc = nowUtc;
            project.ProcessingStartedAtUtc = null;
            project.FinishedAtUtc = null;
            project.FailureReason = null;

            if (_artifactStore.StoredZipExists(project.StoredZipRelativePath))
                _artifactStore.TryDeleteExtractedProjectDirectory(project.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
    }
}
