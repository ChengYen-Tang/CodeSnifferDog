using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectReviewAgentTeamWorkerFactory(
    IProjectReviewAgentTeamDependenciesFactory dependenciesFactory) : IProjectReviewAgentTeamWorkerFactory
{
    private readonly IProjectReviewAgentTeamDependenciesFactory _dependenciesFactory = dependenciesFactory;
    private readonly WorkerFactory _workerFactory = DefaultWorkerFactory;

    internal ProjectReviewAgentTeamWorkerFactory(
        IProjectReviewAgentTeamDependenciesFactory dependenciesFactory,
        WorkerFactory workerFactory)
        : this(dependenciesFactory)
    {
        _workerFactory = workerFactory;
    }

    public IProjectReviewAgentTeamWorker CreateWorker(
        IChatClient chatClient,
        string repositoryRootPath,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        ReviewAgentTeamDependencies dependencies = _dependenciesFactory.CreateDependencies(
            chatClient,
            executionOptions,
            agentEventBus);

        ReviewAgentTeamWorker worker = _workerFactory(
            dependencies,
            repositoryRootPath,
            MapRules(rules),
            MapExecutionOptions(executionOptions));

        return new ProjectReviewAgentTeamWorker(worker);
    }

    internal delegate ReviewAgentTeamWorker WorkerFactory(
        ReviewAgentTeamDependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        ReviewAgentTeamExecutionOptions executionOptions);

    private static ReviewAgentTeamWorker DefaultWorkerFactory(
        ReviewAgentTeamDependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        ReviewAgentTeamExecutionOptions executionOptions) =>
        new ReviewAgentTeamFactory(dependencies).CreateWorker(repositoryRootPath, ruleDefinitions, executionOptions);

    private static ReviewAgentRuleDefinition[] MapRules(IReadOnlyList<ProjectExecutionRuleDefinition> rules) =>
        [.. rules.Select(rule => new ReviewAgentRuleDefinition
        {
            RuleKey = rule.RuleKey,
            RuleMarkdown = rule.RuleMarkdown,
        })];

    private static ReviewAgentTeamExecutionOptions MapExecutionOptions(ExecutionOptions executionOptions) =>
        new()
        {
            MaxParallelAgents = executionOptions.MaxParallelAgents,
            ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
            ContextCompactionMode = executionOptions.ContextCompactionMode,
        };
}
