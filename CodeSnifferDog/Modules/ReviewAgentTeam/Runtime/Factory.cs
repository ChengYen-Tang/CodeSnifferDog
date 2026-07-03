using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Runtime;

public sealed class Factory(Dependencies dependencies)
{
    private readonly Dependencies _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));

    public Worker CreateWorker(
        string repositoryRootPath,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        ExecutionOptions executionOptions) =>
        new(repositoryRootPath, ruleDefinitions, executionOptions, _dependencies);
}
