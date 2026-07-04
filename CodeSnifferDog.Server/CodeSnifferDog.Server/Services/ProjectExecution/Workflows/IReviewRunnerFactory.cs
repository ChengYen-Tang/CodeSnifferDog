using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using Microsoft.Extensions.AI;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Creates the workflow delegates required to execute a full review run.
/// </summary>
internal interface IReviewRunnerFactory
{
    /// <summary>
    /// Creates the scan, project-plan, and rule-flow runners that share the same execution context.
    /// </summary>
    /// <param name="chatClient">Chat client used by the workflow agents.</param>
    /// <param name="executionOptions">Execution limits applied to each workflow.</param>
    /// <param name="compactionSettings">Compaction settings for each workflow stage.</param>
    /// <param name="ruleReviewIssueStore">Issue store that receives rule-review findings.</param>
    /// <param name="ruleReportIssueStore">Issue store that receives report findings.</param>
    /// <param name="agentEventBus">Event bus that receives workflow agent events.</param>
    /// <returns>The workflow delegates used by the hosted execution pipeline.</returns>
    ReviewRunners CreateRunners(
        IChatClient chatClient,
        ExecutionOptions executionOptions,
        Settings compactionSettings,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore,
        IAgentEventBus agentEventBus);
}
