using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Runtime;

/// <summary>
/// Creates review-agent-team workers from a shared dependency bundle.
/// </summary>
/// <param name="dependencies">Shared workflow, store, event-bus, and cleanup dependencies injected into each worker.</param>
public sealed class Factory(Dependencies dependencies)
{
    private readonly Dependencies _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));

    /// <summary>
    /// Creates a worker bound to one repository, one rule set, and one execution configuration.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that the worker should analyze.</param>
    /// <param name="ruleDefinitions">Rule definitions that drive review-stage execution and report generation.</param>
    /// <param name="executionOptions">Execution settings such as parallelism and model context size.</param>
    /// <returns>A worker configured with the shared dependency bundle supplied to this factory.</returns>
    public Worker CreateWorker(
        string repositoryRootPath,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        ExecutionOptions executionOptions) =>
        new(repositoryRootPath, ruleDefinitions, executionOptions, _dependencies);
}
