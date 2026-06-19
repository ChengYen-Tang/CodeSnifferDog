using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

internal sealed class ClaimExecutor(
    IServiceScopeFactory serviceScopeFactory,
    IExecutionArtifactStore artifactStore,
    IExecutionStateService executionStateService,
    ILogger<ClaimExecutor> logger) : IClaimExecutor
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IExecutionArtifactStore _artifactStore = artifactStore;
    private readonly IExecutionStateService _executionStateService = executionStateService;
    private readonly ILogger<ClaimExecutor> _logger = logger;

    public async Task ExecuteAsync(
        int workerNumber,
        ProjectExecutionClaim claim,
        CancellationToken stoppingToken)
    {
        using ProjectExecutionLease lease = claim.ExecutionLease;

        try
        {
            await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
            IProjectAnalysisRunner analysisRunner = scope.ServiceProvider.GetRequiredService<IProjectAnalysisRunner>();

            if (!await _executionStateService.CanStartExecutionAsync(claim.ProjectId, lease.CancellationToken))
            {
                _artifactStore.TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);
                _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
                return;
            }

            string repositoryRootPath = _artifactStore.PrepareRepository(claim);
            _logger.LogInformation(
                "Project executor worker {WorkerNumber} prepared project {ProjectId} at {RepositoryRootPath}.",
                workerNumber,
                claim.ProjectId,
                repositoryRootPath);

            await analysisRunner.RunAsync(new ProjectAnalysisContext
            {
                ProjectId = claim.ProjectId,
                RepositoryRootPath = repositoryRootPath,
            }, lease.CancellationToken);

            if (lease.CancellationToken.IsCancellationRequested)
            {
                await ApplyCancellationOutcomeAsync(claim, lease);
                return;
            }

            await _executionStateService.CompleteAsync(
                claim.ProjectId,
                ProjectProcessingStatus.Completed,
                failureReason: null,
                CancellationToken.None);
            _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
        }
        catch (OperationCanceledException) when (lease.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Project {ProjectId} execution was canceled.", claim.ProjectId);
            await ApplyCancellationOutcomeAsync(claim, lease);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Project {ProjectId} execution failed.", claim.ProjectId);
            _artifactStore.TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);
            _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
            await _executionStateService.CompleteAsync(
                claim.ProjectId,
                ProjectProcessingStatus.Failed,
                exception.Message,
                CancellationToken.None);
        }
    }

    private async Task ApplyCancellationOutcomeAsync(ProjectExecutionClaim claim, ProjectExecutionLease lease)
    {
        ProjectExecutionCancellationOutcome outcome = ProjectExecutionCancellationPolicy.Resolve(lease);

        if (outcome.ShouldDeleteUploadedZip)
            _artifactStore.TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);

        if (outcome.ShouldUpdateDatabase)
            await _executionStateService.CompleteAsync(
                claim.ProjectId,
                ProjectProcessingStatus.Canceled,
                failureReason: null,
                CancellationToken.None);

        if (outcome.ShouldDeleteExtractedProject)
            _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
    }
}
