using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.Scan;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IScanRunnerFactory
{
    Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        OperationalContextCompactionOptions compactionOptions);
}
