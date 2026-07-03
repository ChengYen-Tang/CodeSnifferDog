using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

internal sealed class StateService(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    ILiveUpdateNotifier projectAgentStatusLiveUpdateNotifier,
    IProjectStatusMapper projectStatusMapper) : IStateService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILiveUpdateNotifier _projectAgentStatusLiveUpdateNotifier = projectAgentStatusLiveUpdateNotifier;
    private readonly IProjectStatusMapper _projectStatusMapper = projectStatusMapper;

    public async Task<bool> CanStartExecutionAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        ProjectProcessingStatus? status = await dbContext.Projects
            .Where(project => project.Id == projectId)
            .Select(project => (ProjectProcessingStatus?)project.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return status == ProjectProcessingStatus.Reviewing;
    }

    public async Task CompleteAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

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
        await PublishStatusUpdateAsync(projectId, status, CancellationToken.None);
        await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
    }

    public Task PublishStatusUpdateAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        CancellationToken cancellationToken)
    {
        return _projectAgentStatusLiveUpdateNotifier.NotifyAsync(
            new LiveUpdateDto
            {
                ProjectId = projectId,
                Kind = LiveUpdateKind.ProjectStatusChanged,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                ProjectStatus = new ExecutionStatusChangedDto
                {
                    Status = _projectStatusMapper.Map(status, ProjectStatusMappingExceptionStyle.Persisted),
                },
            },
            cancellationToken);
    }
}
