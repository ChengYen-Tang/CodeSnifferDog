using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using FluentResults;

namespace CodeSnifferDog.Workflows.Preparation;

internal sealed class RepositoryPreparationWorkflow(
    Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> scanWorkflowRunner,
    Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> projectPlanWorkflowRunner,
    IReviewAgentConcurrencyGate concurrencyGate)
{
    private readonly Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> _scanWorkflowRunner = scanWorkflowRunner;
    private readonly Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> _projectPlanWorkflowRunner = projectPlanWorkflowRunner;
    private readonly IReviewAgentConcurrencyGate _concurrencyGate = concurrencyGate;

    public async Task<Result<RepositoryPreparationWorkflowResult>> RunAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RepositoryPreparationWorkflowResult>("Repository root path is required.");

        repositoryRootPath = repositoryRootPath.Trim();

        Result<ScanWorkflowResult> scanResult = await _scanWorkflowRunner(repositoryRootPath, cancellationToken).ConfigureAwait(false);

        if (scanResult.IsFailed)
            return scanResult.ToResult<RepositoryPreparationWorkflowResult>();

        if (!scanResult.Value.ShouldEnterProjectPlanning)
        {
            return Result.Ok(new RepositoryPreparationWorkflowResult
            {
                ScanResult = scanResult.Value,
                ProjectPlanResults = [],
                ShouldEnterRuleReview = false,
            });
        }

        ProjectPlanWorkflowResult[] orderedResults = new ProjectPlanWorkflowResult[scanResult.Value.Projects.Count];
        List<IError> errors = [];

        Task[] tasks =
            [.. scanResult.Value.Projects.Select((project, index) => RunProjectPlanAsync(project, index, orderedResults, errors, repositoryRootPath, cancellationToken))];

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (errors.Count > 0)
            return Result.Fail<RepositoryPreparationWorkflowResult>(errors);

        return Result.Ok(new RepositoryPreparationWorkflowResult
        {
            ScanResult = scanResult.Value,
            ProjectPlanResults = orderedResults,
            ShouldEnterRuleReview = orderedResults.All(result => result.ShouldEnterRuleReview),
        });
    }

    private async Task RunProjectPlanAsync(
        StoredScanProject project,
        int index,
        ProjectPlanWorkflowResult[] orderedResults,
        List<IError> errors,
        string repositoryRootPath,
        CancellationToken cancellationToken)
    {
        await using IAsyncDisposable lease = await _concurrencyGate.AcquireAsync(cancellationToken).ConfigureAwait(false);

        Result<ProjectPlanWorkflowResult> result =
            await _projectPlanWorkflowRunner(repositoryRootPath, project, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            orderedResults[index] = result.Value;
            return;
        }

        lock (errors)
        {
            foreach (IError error in result.Errors)
                errors.Add(error);
        }
    }
}
