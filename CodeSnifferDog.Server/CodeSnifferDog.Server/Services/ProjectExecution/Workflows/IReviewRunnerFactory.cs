using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IReviewRunnerFactory
{
    ReviewRunners CreateRunners(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        Settings compactionSettings,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus);
}
