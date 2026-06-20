using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using IProjectAgentStatusLiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.IProjectAgentStatusLiveUpdateNotifier;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusRuntimeComponentsFactory(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
    IAgentStatusProjectionMapper projectionMapper,
    IAgentTimelinePersistenceService timelinePersistenceService) : IAgentStatusRuntimeComponentsFactory
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _liveUpdateNotifier = liveUpdateNotifier;
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;
    private readonly IAgentTimelinePersistenceService _timelinePersistenceService = timelinePersistenceService;

    public AgentStatusRuntimeComponents Create(Guid projectId)
    {
        AgentStatusLiveUpdateFactory liveUpdateFactory = new(_projectionMapper);
        AgentStatusPersistenceService persistenceService = new(
            projectId,
            _dbContextFactory,
            _liveUpdateNotifier,
            liveUpdateFactory,
            _timelinePersistenceService);

        return new AgentStatusRuntimeComponents(new AgentStatusEventHandler(persistenceService));
    }
}
