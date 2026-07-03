using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using Microsoft.Extensions.AI;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.InMemoryIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.InMemoryIssueStore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal sealed class DependenciesFactory(
    OptionsFactory compactionOptionsFactory,
    IReviewRunnerFactory workflowRunnerFactory) : IDependenciesFactory
{
    private readonly OptionsFactory _compactionOptionsFactory = compactionOptionsFactory;
    private readonly IReviewRunnerFactory _workflowRunnerFactory = workflowRunnerFactory;

    public Dependencies CreateDependencies(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        IAgentEventBus agentEventBus)
    {
        ReviewIssueStore ruleReviewIssueStore = new();
        ReportIssueStore ruleReportIssueStore = new();
        Settings compactionSettings =
            _compactionOptionsFactory.Create(executionOptions);
        ReviewRunners workflowRunners = _workflowRunnerFactory.CreateRunners(
            chatClient,
            executionOptions,
            compactionSettings,
            ruleReviewIssueStore,
            ruleReportIssueStore,
            agentEventBus);

        return new Dependencies
        {
            ScanWorkflowRunner = workflowRunners.ScanWorkflowRunner,
            ProjectPlanWorkflowRunner = workflowRunners.ProjectPlanWorkflowRunner,
            RuleFlowWorkflowRunner = workflowRunners.RuleFlowWorkflowRunner,
            RuleReportIssueStore = ruleReportIssueStore,
            AgentEventBus = agentEventBus,
        };
    }
}
