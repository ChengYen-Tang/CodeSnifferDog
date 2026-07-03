using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;
using AnalysisRuleDefinition = CodeSnifferDog.Server.Services.ProjectExecution.Analysis.RuleDefinition;
using TeamFactory = CodeSnifferDog.Modules.ReviewAgentTeam.Runtime.Factory;
using TeamExecutionOptions = CodeSnifferDog.Models.ReviewAgentTeam.Runtime.ExecutionOptions;
using TeamRuleDefinition = CodeSnifferDog.Models.ReviewAgentTeam.Agents.RuleDefinition;
using TeamWorker = CodeSnifferDog.Modules.ReviewAgentTeam.Runtime.Worker;

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
        IReadOnlyList<AnalysisRuleDefinition> rules,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        Dependencies dependencies = _dependenciesFactory.CreateDependencies(
            chatClient,
            executionOptions,
            agentEventBus);

        TeamWorker worker = _workerFactory(
            dependencies,
            repositoryRootPath,
            MapRules(rules),
            MapExecutionOptions(executionOptions));

        return new Worker(worker);
    }

    internal delegate TeamWorker CreateWorkerDelegate(
        Dependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<TeamRuleDefinition> ruleDefinitions,
        TeamExecutionOptions executionOptions);

    private static TeamWorker DefaultWorkerFactory(
        Dependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<TeamRuleDefinition> ruleDefinitions,
        TeamExecutionOptions executionOptions) =>
        new TeamFactory(dependencies).CreateWorker(repositoryRootPath, ruleDefinitions, executionOptions);

    private static TeamRuleDefinition[] MapRules(IReadOnlyList<AnalysisRuleDefinition> rules) =>
        [.. rules.Select(rule => new TeamRuleDefinition
        {
            RuleKey = rule.RuleKey,
            RuleMarkdown = rule.RuleMarkdown,
        })];

    private static TeamExecutionOptions MapExecutionOptions(ExecutionOptions executionOptions) =>
        new()
        {
            MaxParallelAgents = executionOptions.MaxParallelAgents,
            ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
            ContextCompactionMode = executionOptions.ContextCompactionMode,
        };
}
