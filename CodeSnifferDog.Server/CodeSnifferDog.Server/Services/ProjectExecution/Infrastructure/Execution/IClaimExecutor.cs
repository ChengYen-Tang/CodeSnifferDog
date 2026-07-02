using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

internal interface IClaimExecutor
{
    Task ExecuteAsync(
        int workerNumber,
        Claim claim,
        CancellationToken stoppingToken);
}
