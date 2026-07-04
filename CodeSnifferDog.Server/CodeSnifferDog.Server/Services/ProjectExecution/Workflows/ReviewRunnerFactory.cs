using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using Microsoft.Extensions.AI;
using ProjectPlanRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan.IRunnerFactory;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using RuleFlowRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow.IRunnerFactory;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Builds the workflow delegates used by the review execution pipeline.
/// </summary>
internal sealed class ReviewRunnerFactory : IReviewRunnerFactory
{
    private readonly IScanRunnerFactory _scanRunnerFactory;
    private readonly ProjectPlanRunnerFactoryInterface _projectPlanRunnerFactory;
    private readonly RuleFlowRunnerFactoryInterface _ruleFlowRunnerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewRunnerFactory"/> class.
    /// </summary>
    /// <param name="scanRunnerFactory">Factory that creates scan workflow runners.</param>
    /// <param name="projectPlanRunnerFactory">Factory that creates project-plan workflow runners.</param>
    /// <param name="ruleFlowRunnerFactory">Factory that creates rule-flow workflow runners.</param>
    public ReviewRunnerFactory(
        IScanRunnerFactory scanRunnerFactory,
        ProjectPlanRunnerFactoryInterface projectPlanRunnerFactory,
        RuleFlowRunnerFactoryInterface ruleFlowRunnerFactory)
    {
        _scanRunnerFactory = scanRunnerFactory;
        _projectPlanRunnerFactory = projectPlanRunnerFactory;
        _ruleFlowRunnerFactory = ruleFlowRunnerFactory;
    }

    /// <inheritdoc />
    public ReviewRunners CreateRunners(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        Settings compactionSettings,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus)
    {
        PromptAssetReader promptAssetReader = new();
        WorkflowRuntimeContext context = new(
            chatClient,
            executionOptions,
            new AgentOptionsFactory(
                promptAssetReader,
                new ChatClientSummarizer(chatClient)),
            promptAssetReader,
            agentEventBus);

        return new ReviewRunners
        {
            ScanWorkflowRunner = _scanRunnerFactory.CreateRunner(context, compactionSettings.Scan),
            ProjectPlanWorkflowRunner = _projectPlanRunnerFactory.CreateRunner(context, compactionSettings.ProjectPlan),
            RuleFlowWorkflowRunner = _ruleFlowRunnerFactory.CreateRunner(
                context,
                compactionSettings.RuleReview,
                compactionSettings.Report,
                ruleReviewIssueStore,
                ruleReportIssueStore),
        };
    }
}
