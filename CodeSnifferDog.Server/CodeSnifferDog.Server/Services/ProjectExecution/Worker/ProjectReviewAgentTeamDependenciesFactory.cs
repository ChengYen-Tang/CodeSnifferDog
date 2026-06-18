using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker;

internal sealed class ProjectReviewAgentTeamDependenciesFactory(
    ProjectReviewAgentCompactionOptionsFactory compactionOptionsFactory,
    IProjectReviewWorkflowRunnerFactory workflowRunnerFactory) : IProjectReviewAgentTeamDependenciesFactory
{
    private readonly ProjectReviewAgentCompactionOptionsFactory _compactionOptionsFactory = compactionOptionsFactory;
    private readonly IProjectReviewWorkflowRunnerFactory _workflowRunnerFactory = workflowRunnerFactory;

    public ReviewAgentTeamDependencies CreateDependencies(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();
        InMemoryRuleReportIssueStore ruleReportIssueStore = new();
        ProjectReviewAgentCompactionSettings compactionSettings =
            _compactionOptionsFactory.Create(executionOptions);
        ProjectReviewWorkflowRunners workflowRunners = _workflowRunnerFactory.CreateRunners(
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
