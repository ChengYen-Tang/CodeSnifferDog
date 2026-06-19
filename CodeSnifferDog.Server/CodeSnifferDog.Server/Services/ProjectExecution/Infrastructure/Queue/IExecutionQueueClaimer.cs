namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

internal interface IExecutionQueueClaimer
{
    Task<ProjectExecutionClaim?> TryClaimNextAsync(CancellationToken cancellationToken);
}
