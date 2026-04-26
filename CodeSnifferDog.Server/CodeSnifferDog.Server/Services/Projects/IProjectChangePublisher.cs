namespace CodeSnifferDog.Server.Services.Projects;

public interface IProjectChangePublisher
{
    Task PublishProjectsChangedAsync(CancellationToken cancellationToken = default);
}
