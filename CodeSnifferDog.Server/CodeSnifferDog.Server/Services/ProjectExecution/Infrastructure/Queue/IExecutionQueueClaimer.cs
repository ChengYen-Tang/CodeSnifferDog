namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

internal interface IExecutionQueueClaimer
{
    Task<Claim?> TryClaimNextAsync(CancellationToken cancellationToken);
}
