using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using FluentResults;
using PreparationWorkflowResult = CodeSnifferDog.Models.Preparation.WorkflowResult;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;

namespace CodeSnifferDog.Workflows.Preparation;

/// <summary>
/// Runs preparation by scanning the repository, then planning each discovered project under the shared concurrency gate.
/// </summary>
/// <param name="scanWorkflowRunner">Runs the scan workflow for the repository.</param>
/// <param name="projectPlanWorkflowRunner">Runs the project-plan workflow for one scanned project.</param>
/// <param name="concurrencyGate">Gate that limits how many project-plan workflows run concurrently.</param>
internal sealed class Workflow(
    Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> scanWorkflowRunner,
    Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> projectPlanWorkflowRunner,
    IReviewAgentConcurrencyGate concurrencyGate)
{
    private readonly Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> _scanWorkflowRunner = scanWorkflowRunner;
    private readonly Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> _projectPlanWorkflowRunner = projectPlanWorkflowRunner;
    private readonly IReviewAgentConcurrencyGate _concurrencyGate = concurrencyGate;

    /// <summary>
    /// Runs the preparation workflow for one repository.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that should be prepared for review.</param>
    /// <param name="cancellationToken">Cancels the workflow.</param>
    /// <returns>The preparation workflow result.</returns>
    public async Task<Result<PreparationWorkflowResult>> RunAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<PreparationWorkflowResult>("Repository root path is required.");

        repositoryRootPath = repositoryRootPath.Trim();

        Result<ScanWorkflowResult> scanResult = await _scanWorkflowRunner(repositoryRootPath, cancellationToken).ConfigureAwait(false);

        if (scanResult.IsFailed)
            return scanResult.ToResult<PreparationWorkflowResult>();

        if (scanResult.Value.Projects.Count == 0)
        {
            return Result.Ok(new PreparationWorkflowResult
            {
                ScanResult = scanResult.Value,
                ProjectPlanResults = [],
            });
        }

        ProjectPlanWorkflowResult[] orderedResults = new ProjectPlanWorkflowResult[scanResult.Value.Projects.Count];
        List<IError> errors = [];

        Task[] tasks =
            [.. scanResult.Value.Projects.Select((project, index) => RunProjectPlanAsync(project, index, orderedResults, errors, repositoryRootPath, cancellationToken))];

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (errors.Count > 0)
            return Result.Fail<PreparationWorkflowResult>(errors);

        return Result.Ok(new PreparationWorkflowResult
        {
            ScanResult = scanResult.Value,
            ProjectPlanResults = orderedResults,
        });
    }

    /// <summary>
    /// Runs one project-plan workflow under the shared concurrency gate and stores either its ordered result or its errors.
    /// </summary>
    /// <param name="project">Scanned project to plan.</param>
    /// <param name="index">Index in <paramref name="orderedResults" /> where the result should be stored.</param>
    /// <param name="orderedResults">Ordered result buffer for successful project-plan results.</param>
    /// <param name="errors">Shared error list that collects failed project-plan results.</param>
    /// <param name="repositoryRootPath">Repository root path that contains the project.</param>
    /// <param name="cancellationToken">Cancels the workflow.</param>
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
