using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using Microsoft.EntityFrameworkCore;
using IProjectAgentStatusLiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.IProjectAgentStatusLiveUpdateNotifier;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusRuntimeFactory(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
    IAgentStatusProjectionMapper projectionMapper) : IAgentStatusRuntimeFactory
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _liveUpdateNotifier = liveUpdateNotifier;
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;

    public AgentStatusRuntime Create(Guid projectId)
    {
        AgentStatusLiveUpdateFactory liveUpdateFactory = new(_projectionMapper);
        AgentStatusPersistenceService persistenceService = new(
            projectId,
            _dbContextFactory,
            _liveUpdateNotifier,
            liveUpdateFactory);

        return new AgentStatusRuntime(new AgentStatusEventHandler(persistenceService));
    }
}
