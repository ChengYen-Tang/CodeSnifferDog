using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using FluentResults;

namespace CodeSnifferDog.Workflows.Preparation;

public sealed class RepositoryPreparationWorkflow(
    Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> scanWorkflowRunner,
    Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> projectPlanWorkflowRunner,
    RepositoryPreparationWorkflowOptions? options = null)
{
    private readonly Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> _scanWorkflowRunner = scanWorkflowRunner;
    private readonly Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> _projectPlanWorkflowRunner = projectPlanWorkflowRunner;
    private readonly RepositoryPreparationWorkflowOptions _options = options ?? new();

    public async Task<Result<RepositoryPreparationWorkflowResult>> RunAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RepositoryPreparationWorkflowResult>("Repository root path is required.");

        if (_options.MaxConcurrentProjectPlans <= 0)
            return Result.Fail<RepositoryPreparationWorkflowResult>("MaxConcurrentProjectPlans must be greater than zero.");

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
        using SemaphoreSlim semaphore = new(_options.MaxConcurrentProjectPlans);

        Task[] tasks = scanResult.Value.Projects
            .Select((project, index) => RunProjectPlanAsync(project, index, orderedResults, errors, semaphore, repositoryRootPath, cancellationToken))
            .ToArray();

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
        SemaphoreSlim semaphore,
        string repositoryRootPath,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
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
        finally
        {
            semaphore.Release();
        }
    }
}
