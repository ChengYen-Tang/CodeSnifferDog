using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.Report;
using Microsoft.Extensions.AI;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
public sealed class MessageBuilderTests
{
    [TestMethod]
    public void CreateAggregatorMessages_UsesUserRolePrefixAndBoundedTaskContext()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        StoredTaskItem taskItem = CreateTaskItem();

        List<ChatMessage> messages = builder.CreateAggregatorMessages(taskItem, currentFlowIssueCount: 1);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.AggregatorInputPrefix}{Environment.NewLine}{Environment.NewLine}" +
            $"Task item id:{Environment.NewLine}{taskItem.ProjectPlanTaskItemId}{Environment.NewLine}{Environment.NewLine}" +
            $"Scope entry files:{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItem.Files)}{Environment.NewLine}{Environment.NewLine}" +
            $"Verified current-flow issue count:{Environment.NewLine}1",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_UsesUserRolePrefixAndSerializedDiff()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        StoredTaskItem taskItem = CreateTaskItem();
        Diff diff = new()
        {
            CreatedIssues = [CreateReportIssue()],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

        List<ChatMessage> messages = builder.CreateVerifierMessages(taskItem, currentFlowIssueCount: 1, diff);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}" +
            $"Task item id:{Environment.NewLine}{taskItem.ProjectPlanTaskItemId}{Environment.NewLine}{Environment.NewLine}" +
            $"Scope entry files:{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItem.Files)}{Environment.NewLine}{Environment.NewLine}" +
            $"Verified current-flow issue count:{Environment.NewLine}1{Environment.NewLine}{Environment.NewLine}" +
            $"Current report diff:{Environment.NewLine}{CodeSnifferDogJson.Serialize(diff)}",
            messages[0].Text);
    }

    private static StoredTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-1",
            Files =
            [
                new PlanFile
                {
                    FilePath = "Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static ReportStoredIssue CreateReportIssue() =>
        new()
        {
            RuleReportIssueId = "report-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the hot path.",
            Confidence = "High",
            FollowUpFiles = "Program.cs",
            SuggestedFixDirection = "Use a cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
        };
}
