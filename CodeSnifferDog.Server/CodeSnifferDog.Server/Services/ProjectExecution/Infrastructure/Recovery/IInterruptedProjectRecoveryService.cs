namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;

internal interface IInterruptedProjectRecoveryService
{
    Task RecoverAsync(CancellationToken cancellationToken);
}
