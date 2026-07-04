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

/// <summary>
/// Creates review-team workers by adapting project-execution contracts to the runtime model.
/// </summary>
internal sealed class WorkerFactory(
    IDependenciesFactory dependenciesFactory) : IWorkerFactory
{
    private readonly IDependenciesFactory _dependenciesFactory = dependenciesFactory;
    private readonly CreateWorkerDelegate _workerFactory = DefaultWorkerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerFactory"/> class for tests that need a custom worker constructor.
    /// </summary>
    /// <param name="dependenciesFactory">Factory that creates runtime dependencies.</param>
    /// <param name="workerFactory">Delegate that constructs the underlying runtime worker.</param>
    internal WorkerFactory(
        IDependenciesFactory dependenciesFactory,
        CreateWorkerDelegate workerFactory)
        : this(dependenciesFactory)
    {
        _workerFactory = workerFactory;
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Represents the delegate used to construct the underlying review-team runtime worker.
    /// </summary>
    /// <param name="dependencies">Runtime dependencies used by the worker.</param>
    /// <param name="repositoryRootPath">Repository root path to analyze.</param>
    /// <param name="ruleDefinitions">Rule definitions passed to the runtime worker.</param>
    /// <param name="executionOptions">Runtime execution options.</param>
    /// <returns>The constructed runtime worker.</returns>
    internal delegate TeamWorker CreateWorkerDelegate(
        Dependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<TeamRuleDefinition> ruleDefinitions,
        TeamExecutionOptions executionOptions);

    /// <summary>
    /// Creates the default runtime worker implementation.
    /// </summary>
    /// <param name="dependencies">Runtime dependencies used by the worker.</param>
    /// <param name="repositoryRootPath">Repository root path to analyze.</param>
    /// <param name="ruleDefinitions">Rule definitions passed to the runtime worker.</param>
    /// <param name="executionOptions">Runtime execution options.</param>
    /// <returns>The constructed runtime worker.</returns>
    private static TeamWorker DefaultWorkerFactory(
        Dependencies dependencies,
        string repositoryRootPath,
        IReadOnlyList<TeamRuleDefinition> ruleDefinitions,
        TeamExecutionOptions executionOptions) =>
        new TeamFactory(dependencies).CreateWorker(repositoryRootPath, ruleDefinitions, executionOptions);

    /// <summary>
    /// Maps project-execution rule definitions to review-team runtime rule definitions.
    /// </summary>
    /// <param name="rules">Project-execution rule definitions.</param>
    /// <returns>The mapped runtime rule definitions.</returns>
    private static TeamRuleDefinition[] MapRules(IReadOnlyList<AnalysisRuleDefinition> rules) =>
        [.. rules.Select(rule => new TeamRuleDefinition
        {
            RuleKey = rule.RuleKey,
            RuleMarkdown = rule.RuleMarkdown,
        })];

    /// <summary>
    /// Maps project-execution worker options to review-team runtime execution options.
    /// </summary>
    /// <param name="executionOptions">Project-execution worker options.</param>
    /// <returns>The mapped runtime execution options.</returns>
    private static TeamExecutionOptions MapExecutionOptions(ExecutionOptions executionOptions) =>
        new()
        {
            MaxParallelAgents = executionOptions.MaxParallelAgents,
            ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
            ContextCompactionMode = executionOptions.ContextCompactionMode,
        };
}
