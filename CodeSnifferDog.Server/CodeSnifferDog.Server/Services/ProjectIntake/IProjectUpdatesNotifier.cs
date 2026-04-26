namespace CodeSnifferDog.Server.Services.ProjectIntake;

public interface IProjectUpdatesNotifier
{
    Task NotifyProjectsChangedAsync(CancellationToken cancellationToken = default);
}
