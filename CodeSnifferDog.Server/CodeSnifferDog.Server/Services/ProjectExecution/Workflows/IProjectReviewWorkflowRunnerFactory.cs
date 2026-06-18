using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IProjectReviewWorkflowRunnerFactory
{
    ProjectReviewWorkflowRunners CreateRunners(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        ProjectReviewAgentCompactionSettings compactionSettings,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus);
}
