using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class ReviewAgentTeamFactory(ReviewAgentTeamDependencies dependencies)
{
    private readonly ReviewAgentTeamDependencies _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));

    public ReviewAgentTeamWorker CreateWorker(
        string repositoryRootPath,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        ReviewAgentTeamExecutionOptions executionOptions) =>
        new(repositoryRootPath, ruleDefinitions, executionOptions, _dependencies);
}
