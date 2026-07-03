using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

internal sealed class ClaimExecutor(
    IServiceScopeFactory serviceScopeFactory,
    IExecutionArtifactStore artifactStore,
    IStateService StateService,
    ILogger<ClaimExecutor> logger) : IClaimExecutor
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IExecutionArtifactStore _artifactStore = artifactStore;
    private readonly IStateService _StateService = StateService;
    private readonly ILogger<ClaimExecutor> _logger = logger;

    public async Task ExecuteAsync(
        int workerNumber,
        Claim claim,
        CancellationToken stoppingToken)
    {
        using Lease lease = claim.ExecutionLease;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
            IProjectAnalysisRunner analysisRunner = scope.ServiceProvider.GetRequiredService<IProjectAnalysisRunner>();

            _logger.LogInformation(
                "Project executor worker {WorkerNumber} started project {ProjectId}.",
                workerNumber,
                claim.ProjectId);

            if (!await _StateService.CanStartExecutionAsync(claim.ProjectId, lease.CancellationToken))
            {
                _logger.LogWarning(
                    "Project executor worker {WorkerNumber} skipped project {ProjectId} because execution state cannot start.",
                    workerNumber,
                    claim.ProjectId);
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
                await ApplyCancellationOutcomeAsync(claim, lease, stopwatch.ElapsedMilliseconds);
                return;
            }

            await _StateService.CompleteAsync(
                claim.ProjectId,
                ProjectProcessingStatus.Completed,
                failureReason: null,
                CancellationToken.None);
            _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
            _logger.LogInformation(
                "Project {ProjectId} execution completed in {DurationMs} ms.",
                claim.ProjectId,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (lease.CancellationToken.IsCancellationRequested)
        {
            await ApplyCancellationOutcomeAsync(claim, lease, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Project {ProjectId} execution failed after {DurationMs} ms.",
                claim.ProjectId,
                stopwatch.ElapsedMilliseconds);
            _artifactStore.TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);
            _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
            await _StateService.CompleteAsync(
                claim.ProjectId,
                ProjectProcessingStatus.Failed,
                exception.Message,
                CancellationToken.None);
        }
    }

    private async Task ApplyCancellationOutcomeAsync(
        Claim claim,
        Lease lease,
        long durationMs)
    {
        Outcome outcome = Policy.Resolve(lease);

        _logger.LogInformation(
            "Project {ProjectId} execution was canceled after {DurationMs} ms.",
            claim.ProjectId,
            durationMs);

        if (outcome.ShouldDeleteUploadedZip)
            _artifactStore.TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);

        if (outcome.ShouldUpdateDatabase)
            await _StateService.CompleteAsync(
                claim.ProjectId,
                ProjectProcessingStatus.Canceled,
                failureReason: null,
                CancellationToken.None);

        if (outcome.ShouldDeleteExtractedProject)
            _artifactStore.TryDeleteExtractedProjectDirectory(claim.ProjectId);
    }
}
