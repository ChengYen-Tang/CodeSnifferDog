using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan;

/// <summary>
/// Creates the workflow delegate that produces project-plan tasks from scan results.
/// </summary>
internal interface IRunnerFactory
{
    /// <summary>
    /// Creates the project-plan workflow delegate for a specific runtime context and compaction profile.
    /// </summary>
    /// <param name="context">Shared runtime services for the workflow execution.</param>
    /// <param name="compactionOptions">Compaction behavior applied to project-plan agents.</param>
    /// <returns>A delegate that transforms scan results into project-plan tasks.</returns>
    Func<string, StoredScanProject, CancellationToken, Task<Result<WorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions compactionOptions);
}
