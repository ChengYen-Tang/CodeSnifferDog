using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal sealed class WorkerFactory(
    IDependenciesFactory dependenciesFactory) : IWorkerFactory
{
    private readonly IDependenciesFactory _dependenciesFactory = dependenciesFactory;
    private readonly CreateWorkerDelegate _workerFactory = DefaultWorkerFactory;

    internal WorkerFactory(
        IDependenciesFactory dependenciesFactory,
        CreateWorkerDelegate workerFactory)
        : this(dependenciesFactory)
    {
        _workerFactory = workerFactory;
    }

    public IWorker CreateWorker(
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

        return new Worker(worker);
    }

    internal delegate ReviewAgentTeamWorker CreateWorkerDelegate(
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
