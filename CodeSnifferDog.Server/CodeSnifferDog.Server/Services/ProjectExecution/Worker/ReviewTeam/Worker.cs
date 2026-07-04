using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using TeamWorker = CodeSnifferDog.Modules.ReviewAgentTeam.Runtime.Worker;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

/// <summary>
/// Adapts the review-team runtime worker to the project-execution worker contract.
/// </summary>
internal sealed class Worker(TeamWorker innerWorker) : IWorker
{
    private readonly TeamWorker _innerWorker = innerWorker;

    /// <inheritdoc />
    public Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default) =>
        _innerWorker.AnalyzeDetailedAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _innerWorker.DisposeAsync();
}
