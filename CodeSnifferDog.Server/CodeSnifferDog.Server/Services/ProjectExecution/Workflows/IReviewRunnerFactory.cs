using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using Microsoft.Extensions.AI;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IReviewRunnerFactory
{
    ReviewRunners CreateRunners(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        Settings compactionSettings,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus);
}
