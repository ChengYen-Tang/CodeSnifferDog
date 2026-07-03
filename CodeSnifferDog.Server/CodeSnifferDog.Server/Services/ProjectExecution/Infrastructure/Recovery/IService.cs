namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;

internal interface IService
{
    Task RecoverAsync(CancellationToken cancellationToken);
}
