using CodeSnifferDog.Models.Scan;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IScanRunnerFactory
{
    Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions compactionOptions);
}
