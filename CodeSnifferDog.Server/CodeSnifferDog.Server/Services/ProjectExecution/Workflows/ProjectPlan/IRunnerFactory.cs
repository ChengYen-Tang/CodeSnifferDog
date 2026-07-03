using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan;

internal interface IRunnerFactory
{
    Func<string, StoredScanProject, CancellationToken, Task<Result<WorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions compactionOptions);
}
