namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

internal interface IClaimer
{
    Task<Claim?> TryClaimNextAsync(CancellationToken cancellationToken);
}
