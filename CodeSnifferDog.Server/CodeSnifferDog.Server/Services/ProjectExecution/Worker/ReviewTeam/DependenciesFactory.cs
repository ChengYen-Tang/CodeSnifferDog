using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal sealed class DependenciesFactory(
    OptionsFactory compactionOptionsFactory,
    IReviewRunnerFactory workflowRunnerFactory) : IDependenciesFactory
{
    private readonly OptionsFactory _compactionOptionsFactory = compactionOptionsFactory;
    private readonly IReviewRunnerFactory _workflowRunnerFactory = workflowRunnerFactory;

    public ReviewAgentTeamDependencies CreateDependencies(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();
        InMemoryRuleReportIssueStore ruleReportIssueStore = new();
        Settings compactionSettings =
            _compactionOptionsFactory.Create(executionOptions);
        ReviewRunners workflowRunners = _workflowRunnerFactory.CreateRunners(
            chatClient,
            executionOptions,
            compactionSettings,
            ruleReviewIssueStore,
            ruleReportIssueStore,
            agentEventBus);

        return new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = workflowRunners.ScanWorkflowRunner,
            ProjectPlanWorkflowRunner = workflowRunners.ProjectPlanWorkflowRunner,
            RuleFlowWorkflowRunner = workflowRunners.RuleFlowWorkflowRunner,
            RuleReportIssueStore = ruleReportIssueStore,
            AgentEventBus = agentEventBus,
        };
    }
}
