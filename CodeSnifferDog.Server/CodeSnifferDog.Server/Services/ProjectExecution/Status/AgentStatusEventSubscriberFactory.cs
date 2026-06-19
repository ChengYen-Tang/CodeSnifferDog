using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using Microsoft.EntityFrameworkCore;
using IProjectAgentStatusLiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.IProjectAgentStatusLiveUpdateNotifier;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusEventSubscriberFactory(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
    IAgentStatusProjectionMapper projectionMapper) : IAgentStatusEventSubscriberFactory
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _liveUpdateNotifier = liveUpdateNotifier;
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;

    public ProjectAgentStatusEventSubscriber Create(
        Guid projectId,
        IObservable<AgentStatusEvent> events)
    {
        AgentStatusPersistenceService persistenceService = new(
            projectId,
            _dbContextFactory,
            _liveUpdateNotifier,
            new AgentStatusLiveUpdateFactory(_projectionMapper));

        return new ProjectAgentStatusEventSubscriber(
            new AgentStatusEventHandler(persistenceService),
            events);
    }
}
