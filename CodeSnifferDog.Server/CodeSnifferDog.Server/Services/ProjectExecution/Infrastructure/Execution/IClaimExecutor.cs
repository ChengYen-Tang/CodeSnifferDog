using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

/// <summary>
/// Executes a claimed project on behalf of a background worker.
/// </summary>
internal interface IClaimExecutor
{
    /// <summary>
    /// Executes the claimed project.
    /// </summary>
    /// <param name="workerNumber">One-based worker number used in logs.</param>
    /// <param name="claim">Claim that identifies the project and execution lease.</param>
    /// <param name="stoppingToken">Host token that stops the worker.</param>
    Task ExecuteAsync(
        int workerNumber,
        Claim claim,
        CancellationToken stoppingToken);
}
