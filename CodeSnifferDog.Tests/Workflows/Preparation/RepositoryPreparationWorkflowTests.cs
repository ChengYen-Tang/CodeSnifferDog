using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Workflows.Preparation;
using FluentResults;

namespace CodeSnifferDog.Tests.Workflows.Preparation;

[TestClass]
public sealed class RepositoryPreparationWorkflowTests
{
    public required TestContext TestContext { get; init; }

    private static readonly string[] PlannedProjectIds =
    [
        "scan-1",
        "scan-2",
    ];

    private static readonly string[] OrderedProjectIds =
    [
        "scan-1",
        "scan-2",
        "scan-3",
    ];

    [TestMethod]
    public async Task RunAsync_RunsProjectPlanWorkflow_ForEachScannedProject()
    {
        List<string> plannedProjectIds = [];
        RepositoryPreparationWorkflow workflow = new(
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(
                CreateScanProject("scan-1", "ProjectOne"),
                CreateScanProject("scan-2", "ProjectTwo")))),
            (repositoryRootPath, scanProject, cancellationToken) =>
            {
                plannedProjectIds.Add(scanProject.ScanProjectId);
                return Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<RepositoryPreparationWorkflowResult> result =
            await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(PlannedProjectIds, plannedProjectIds);
        Assert.HasCount(2, result.Value.ProjectPlanResults);
    }

    [TestMethod]
    public async Task RunAsync_PreservesScanProjectOrder_InProjectPlanResults()
    {
        RepositoryPreparationWorkflow workflow = new(
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(
                CreateScanProject("scan-1", "ProjectOne"),
                CreateScanProject("scan-2", "ProjectTwo"),
                CreateScanProject("scan-3", "ProjectThree")))),
            async (repositoryRootPath, scanProject, cancellationToken) =>
            {
                if (scanProject.ScanProjectId == "scan-1")
                    await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                else if (scanProject.ScanProjectId == "scan-2")
                    await Task.Delay(5, cancellationToken).ConfigureAwait(false);

                return Result.Ok(CreateProjectPlanResult(scanProject));
            },
            new ReviewAgentConcurrencyGate(3));

        Result<RepositoryPreparationWorkflowResult> result =
            await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(
            OrderedProjectIds,
            result.Value.ProjectPlanResults.Select(project => project.ScanProject.ScanProjectId).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_SkipsProjectPlanning_WhenScanResultDoesNotAdvance()
    {
        bool projectPlanCalled = false;
        RepositoryPreparationWorkflow workflow = new(
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(new ScanWorkflowResult
            {
                Projects = [],
                Verdict = new ReviewVerdict
                {
                    Approved = false,
                    Message = "Do not continue.",
                },
                ScanAttempts = 1,
                VerifierAttempts = 1,
                ScanAgentResetCount = 0,
            })),
            (repositoryRootPath, scanProject, cancellationToken) =>
            {
                projectPlanCalled = true;
                return Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject)));
            },
            new ReviewAgentConcurrencyGate(2));

        Result<RepositoryPreparationWorkflowResult> result =
            await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(projectPlanCalled);
        Assert.IsEmpty(result.Value.ProjectPlanResults);
    }

    [TestMethod]
    public async Task RunAsync_PreservesSuccessfulProjectPlans_EvenWhenAllTaskItemListsAreEmpty()
    {
        RepositoryPreparationWorkflow workflow = new(
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(
                CreateScanProject("scan-1", "ProjectOne"),
                CreateScanProject("scan-2", "ProjectTwo")))),
            (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject, taskItems: []))),
            new ReviewAgentConcurrencyGate(2));

        Result<RepositoryPreparationWorkflowResult> result =
            await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.HasCount(2, result.Value.ProjectPlanResults);
        Assert.IsTrue(result.Value.ProjectPlanResults.All(projectPlan => projectPlan.TaskItems.Count == 0));
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenAnyProjectPlanWorkflowFails()
    {
        RepositoryPreparationWorkflow workflow = new(
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(
                CreateScanProject("scan-1", "ProjectOne"),
                CreateScanProject("scan-2", "ProjectTwo")))),
            (repositoryRootPath, scanProject, cancellationToken) =>
            {
                if (scanProject.ScanProjectId == "scan-2")
                    return Task.FromResult(Result.Fail<ProjectPlanWorkflowResult>("ProjectTwo planning failed."));

                return Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject)));
            },
            new ReviewAgentConcurrencyGate(2));

        Result<RepositoryPreparationWorkflowResult> result =
            await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("ProjectTwo planning failed.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Constructor_Throws_WhenParallelismIsInvalid()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ReviewAgentConcurrencyGate(0));
    }

    private static ScanWorkflowResult CreateScanResult(params StoredScanProject[] projects)
        =>
        new()
        {
            Projects = projects,
            Verdict = new ReviewVerdict
            {
                Approved = true,
                Message = "Scan complete.",
            },
            ScanAttempts = 1,
            VerifierAttempts = 1,
            ScanAgentResetCount = 0,
        };

    private static StoredScanProject CreateScanProject(string id, string name)
        =>
        new()
        {
            ScanProjectId = id,
            ProjectName = name,
            ProjectPath = $"src/{name}/{name}.csproj",
            ProjectType = ".csproj",
            Reason = $"Reason for {name}.",
        };

    private static ProjectPlanWorkflowResult CreateProjectPlanResult(
        StoredScanProject scanProject,
        IReadOnlyList<StoredProjectPlanTaskItem>? taskItems = null)
        =>
        new()
        {
            ScanProject = scanProject,
            TaskItems = taskItems ??
                [
                    new StoredProjectPlanTaskItem
                    {
                        ProjectPlanTaskItemId = $"task-{scanProject.ScanProjectId}",
                        Files =
                        [
                            new ProjectPlanFile
                            {
                                FilePath = $"src/{scanProject.ProjectName}/Program.cs",
                                TotalLines = 100,
                            },
                        ],
                    },
                ],
            Verdict = new ReviewVerdict
            {
                Approved = true,
                Message = "Plan complete.",
            },
            ContinuedAfterVerifierRejectionLimit = false,
            PlanAttempts = 1,
            VerifierAttempts = 1,
            ProjectPlanAgentResetCount = 0,
        };
}
