using CodeSnifferDog.Models.Scan;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Creates the workflow delegate that scans a repository before review planning starts.
/// </summary>
internal interface IScanRunnerFactory
{
    /// <summary>
    /// Creates the scan workflow delegate for a specific runtime context and compaction profile.
    /// </summary>
    /// <param name="context">Shared runtime services for the workflow execution.</param>
    /// <param name="compactionOptions">Compaction behavior applied to scan agents.</param>
    /// <returns>A delegate that scans a repository root and returns the resulting project list.</returns>
    Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions compactionOptions);
}
