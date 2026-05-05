using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectExecutionHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectExecutionLeaseRegistry leaseRegistry,
    IProjectExecutionQueueLock queueLock,
    IOptions<ProjectExecutionOptions> options,
    ILogger<ProjectExecutionHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectExecutionLeaseRegistry _leaseRegistry = leaseRegistry;
    private readonly IProjectExecutionQueueLock _queueLock = queueLock;
    private readonly ProjectExecutionOptions _options = options.Value;
    private readonly ILogger<ProjectExecutionHostedService> _logger = logger;
    private bool _loggedAnalysisRunnerNotReady;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.MaxConcurrentWorkers <= 0)
            throw new InvalidOperationException("ProjectExecution:MaxConcurrentWorkers must be greater than zero.");

        await FailInterruptedProjectsAsync(stoppingToken);

        Task[] workers = Enumerable
            .Range(0, _options.MaxConcurrentWorkers)
            .Select(workerIndex => RunWorkerLoopAsync(workerIndex + 1, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerLoopAsync(int workerNumber, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!IsAnalysisRunnerReady())
                {
                    if (!_loggedAnalysisRunnerNotReady)
                    {
                        _loggedAnalysisRunnerNotReady = true;
                        _logger.LogInformation("Project executor is waiting for a configured project analysis runner.");
                    }

                    await Task.Delay(_options.QueuePollingInterval, stoppingToken);
                    continue;
                }

                ProjectExecutionClaim? claim;
                using (await _queueLock.AcquireAsync(stoppingToken))
                    claim = await TryClaimNextProjectAsync(stoppingToken);

                if (claim is null)
                {
                    await Task.Delay(_options.QueuePollingInterval, stoppingToken);
                    continue;
                }

                await RunClaimedProjectAsync(workerNumber, claim, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Project executor worker {WorkerNumber} loop failed.", workerNumber);
                await Task.Delay(_options.QueuePollingInterval, stoppingToken);
            }
        }
    }

    private bool IsAnalysisRunnerReady()
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        IProjectAnalysisRunner analysisRunner = scope.ServiceProvider.GetRequiredService<IProjectAnalysisRunner>();
        return analysisRunner.IsReady;
    }

    private async Task<ProjectExecutionClaim?> TryClaimNextProjectAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        ProjectExecutionClaimData? claimData = await TryClaimNextProjectFromDatabaseAsync(cancellationToken);
        if (claimData is null)
            return null;

        ProjectExecutionLease executionLease = _leaseRegistry.Register(claimData.ProjectId, cancellationToken);

        try
        {
            await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);

            return new ProjectExecutionClaim
            {
                ProjectId = claimData.ProjectId,
                StoredZipRelativePath = claimData.StoredZipRelativePath,
                ExecutionLease = executionLease,
            };
        }
        catch
        {
            executionLease.Dispose();
            throw;
        }
    }

    private async Task<ProjectExecutionClaimData?> TryClaimNextProjectFromDatabaseAsync(CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .OrderBy(project => project.Status == ProjectProcessingStatus.Queued ? 0 : 1)
            .ThenBy(project => project.QueueTimestampUtc)
            .ThenBy(project => project.CreatedAtUtc)
            .FirstOrDefaultAsync(project => project.Status == ProjectProcessingStatus.Queued, cancellationToken);

        if (project is null)
            return null;

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        project.Status = ProjectProcessingStatus.Reviewing;
        project.ProcessingStartedAtUtc = nowUtc;
        project.FinishedAtUtc = null;
        project.UpdatedAtUtc = nowUtc;
        project.FailureReason = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProjectExecutionClaimData
        {
            ProjectId = project.Id,
            StoredZipRelativePath = project.StoredZipRelativePath,
        };
    }

    private async Task RunClaimedProjectAsync(
        int workerNumber,
        ProjectExecutionClaim claim,
        CancellationToken stoppingToken)
    {
        using ProjectExecutionLease lease = claim.ExecutionLease;

        try
        {
            await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
            ProjectTemporaryStoragePaths storagePaths = scope.ServiceProvider.GetRequiredService<ProjectTemporaryStoragePaths>();
            IProjectAnalysisRunner analysisRunner = scope.ServiceProvider.GetRequiredService<IProjectAnalysisRunner>();

            if (!await CanStartExecutionAsync(claim.ProjectId, lease.CancellationToken))
            {
                TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);
                TryDeleteExtractedProjectDirectory(claim.ProjectId);
                return;
            }

            string repositoryRootPath = PrepareRepository(claim, storagePaths);
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

            await CompleteProjectAsync(claim.ProjectId, ProjectProcessingStatus.Completed, failureReason: null, CancellationToken.None);
            TryDeleteExtractedProjectDirectory(claim.ProjectId);
        }
        catch (OperationCanceledException) when (lease.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Project {ProjectId} execution was canceled.", claim.ProjectId);
            await ApplyCancellationOutcomeAsync(claim, lease);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Project {ProjectId} execution failed.", claim.ProjectId);
            TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);
            TryDeleteExtractedProjectDirectory(claim.ProjectId);
            await CompleteProjectAsync(claim.ProjectId, ProjectProcessingStatus.Failed, exception.Message, CancellationToken.None);
        }
    }

    private async Task<bool> CanStartExecutionAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectProcessingStatus? status = await dbContext.Projects
            .Where(project => project.Id == projectId)
            .Select(project => (ProjectProcessingStatus?)project.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return status == ProjectProcessingStatus.Reviewing;
    }

    private async Task ApplyCancellationOutcomeAsync(ProjectExecutionClaim claim, ProjectExecutionLease lease)
    {
        ProjectExecutionCancellationOutcome outcome = ProjectExecutionCancellationPolicy.Resolve(lease);

        if (outcome.ShouldDeleteUploadedZip)
            TryDeleteUploadedZipFile(claim.StoredZipRelativePath, claim.ProjectId);

        if (outcome.ShouldUpdateDatabase)
            await CompleteProjectAsync(claim.ProjectId, ProjectProcessingStatus.Canceled, failureReason: null, CancellationToken.None);

        if (outcome.ShouldDeleteExtractedProject)
            TryDeleteExtractedProjectDirectory(claim.ProjectId);
    }

    private static string PrepareRepository(ProjectExecutionClaim claim, ProjectTemporaryStoragePaths storagePaths)
    {
        string uploadedZipPath = storagePaths.ResolveStoredZipPath(claim.StoredZipRelativePath);
        string extractedProjectPath = storagePaths.ResolveExtractedProjectPath(claim.ProjectId);

        if (File.Exists(uploadedZipPath))
        {
            DeleteDirectoryIfExists(extractedProjectPath);
            Directory.CreateDirectory(extractedProjectPath);
            ZipFile.ExtractToDirectory(uploadedZipPath, extractedProjectPath);
            File.Delete(uploadedZipPath);
            return extractedProjectPath;
        }

        if (!Directory.Exists(extractedProjectPath))
            throw new FileNotFoundException("Project upload zip and extracted repository were not found.", uploadedZipPath);

        return extractedProjectPath;
    }

    private async Task CompleteProjectAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return;

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        project.Status = status;
        project.UpdatedAtUtc = nowUtc;
        project.FinishedAtUtc = nowUtc;
        project.FailureReason = failureReason;

        await dbContext.SaveChangesAsync(cancellationToken);
        await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
    }

    private async Task FailInterruptedProjectsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        ProjectTemporaryStoragePaths storagePaths = scope.ServiceProvider.GetRequiredService<ProjectTemporaryStoragePaths>();
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ProjectRecord> interruptedProjects = await dbContext.Projects
            .Where(project => project.Status == ProjectProcessingStatus.Reviewing)
            .ToListAsync(cancellationToken);

        if (interruptedProjects.Count == 0)
            return;

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        foreach (ProjectRecord project in interruptedProjects)
        {
            if (!StoredZipExists(storagePaths, project.StoredZipRelativePath)
                && !Directory.Exists(storagePaths.ResolveExtractedProjectPath(project.Id)))
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

            if (StoredZipExists(storagePaths, project.StoredZipRelativePath))
                TryDeleteDirectory(storagePaths.ResolveExtractedProjectPath(project.Id), project.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }

    private static bool StoredZipExists(ProjectTemporaryStoragePaths storagePaths, string storedZipRelativePath) =>
        File.Exists(storagePaths.ResolveStoredZipPath(storedZipRelativePath));

    private void TryDeleteExtractedProjectDirectory(Guid projectId)
    {
        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            ProjectTemporaryStoragePaths storagePaths = scope.ServiceProvider.GetRequiredService<ProjectTemporaryStoragePaths>();
            TryDeleteDirectory(storagePaths.ResolveExtractedProjectPath(projectId), projectId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete extracted directory for project {ProjectId}.", projectId);
        }
    }

    private void TryDeleteUploadedZipFile(string storedZipRelativePath, Guid projectId)
    {
        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            ProjectTemporaryStoragePaths storagePaths = scope.ServiceProvider.GetRequiredService<ProjectTemporaryStoragePaths>();
            TryDeleteFile(storagePaths.ResolveStoredZipPath(storedZipRelativePath), projectId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete uploaded zip for project {ProjectId}.", projectId);
        }
    }

    private void TryDeleteDirectory(string directoryPath, Guid projectId)
    {
        try
        {
            DeleteDirectoryIfExists(directoryPath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete extracted directory for project {ProjectId}.", projectId);
        }
    }

    private void TryDeleteFile(string filePath, Guid projectId)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete uploaded zip for project {ProjectId}.", projectId);
        }
    }

    private sealed class ProjectExecutionClaim
    {
        public required Guid ProjectId { get; init; }

        public required string StoredZipRelativePath { get; init; }

        public required ProjectExecutionLease ExecutionLease { get; init; }
    }

    private sealed class ProjectExecutionClaimData
    {
        public required Guid ProjectId { get; init; }

        public required string StoredZipRelativePath { get; init; }
    }
}
