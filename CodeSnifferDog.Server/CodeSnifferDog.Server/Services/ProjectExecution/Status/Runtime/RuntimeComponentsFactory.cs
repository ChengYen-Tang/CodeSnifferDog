using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using ILiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications.ILiveUpdateNotifier;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Creates the persistence services that back project-execution status tracking.
/// </summary>
internal sealed class RuntimeComponentsFactory(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    ILiveUpdateNotifier liveUpdateNotifier,
    IProjectionMapper projectionMapper,
    ITimelinePersistenceService timelinePersistenceService) : IRuntimeComponentsFactory
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILiveUpdateNotifier _liveUpdateNotifier = liveUpdateNotifier;
    private readonly IProjectionMapper _projectionMapper = projectionMapper;
    private readonly ITimelinePersistenceService _timelinePersistenceService = timelinePersistenceService;

    /// <inheritdoc />
    public RuntimeComponents Create(Guid projectId)
    {
        LiveUpdateFactory liveUpdateFactory = new(_projectionMapper);
        PersistenceService persistenceService = new(
            projectId,
            _dbContextFactory,
            _liveUpdateNotifier,
            liveUpdateFactory,
            _timelinePersistenceService);

        return new RuntimeComponents(new PersistenceEventHandler(persistenceService));
    }
}
