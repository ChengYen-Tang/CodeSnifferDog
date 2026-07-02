using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.Scan;
using ProjectPlanResultFactory = CodeSnifferDog.Workflows.ProjectPlan.ResultFactory;
using ReportResultFactory = CodeSnifferDog.Workflows.Report.ResultFactory;
using RuleReviewResultFactory = CodeSnifferDog.Workflows.RuleReview.ResultFactory;
using ScanResultFactory = CodeSnifferDog.Workflows.Scan.ResultFactory;

namespace CodeSnifferDog.Tests.Workflows.Common;

[TestClass]
public sealed class WorkflowResultFactoryTests
{
    [TestMethod]
    public void ScanFactory_PopulatesAllResultFields()
    {
        StoredScanProject[] projects = [CreateScanProject()];
        ReviewVerdict verdict = CreateVerdict(approved: true);

        ScanWorkflowResult result = ScanResultFactory.Create(projects, verdict, 2, 3, 1);

        Assert.AreSame(projects, result.Projects);
        Assert.AreSame(verdict, result.Verdict);
        Assert.AreEqual(2, result.ScanAttempts);
        Assert.AreEqual(3, result.VerifierAttempts);
        Assert.AreEqual(1, result.ScanAgentResetCount);
    }

    [TestMethod]
    public void ProjectPlanFactory_PopulatesAllResultFields()
    {
        StoredScanProject scanProject = CreateScanProject();
        StoredProjectPlanTaskItem[] taskItems = [CreateTaskItem()];
        ReviewVerdict verdict = CreateVerdict(approved: false);

        ProjectPlanWorkflowResult result = ProjectPlanResultFactory.Create(
            scanProject,
            taskItems,
            verdict,
            planAttempts: 2,
            verifierAttempts: 3,
            projectPlanAgentResetCount: 1,
            continuedAfterVerifierRejectionLimit: true);

        Assert.AreSame(scanProject, result.ScanProject);
        Assert.AreSame(taskItems, result.TaskItems);
        Assert.AreSame(verdict, result.Verdict);
        Assert.IsTrue(result.ContinuedAfterVerifierRejectionLimit);
        Assert.AreEqual(2, result.PlanAttempts);
        Assert.AreEqual(3, result.VerifierAttempts);
        Assert.AreEqual(1, result.ProjectPlanAgentResetCount);
    }

    [TestMethod]
    public void RuleReviewFactory_PopulatesAllResultFields()
    {
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        StoredRuleReviewIssue[] issues = [CreateReviewIssue()];
        NoIssueConclusion noIssueConclusion = CreateNoIssueConclusion();
        ReviewVerdict verdict = CreateVerdict(approved: false);

        RuleReviewWorkflowResult result = RuleReviewResultFactory.Create(
            taskItem,
            "performance",
            issues,
            noIssueConclusion,
            verdict,
            reviewAttempts: 2,
            verifierAttempts: 3,
            ruleReviewAgentResetCount: 1,
            continuedAfterVerifierRejectionLimit: true,
            stoppedAfterMissingSubmissionLimit: true);

        Assert.AreSame(taskItem, result.TaskItem);
        Assert.AreEqual("performance", result.RuleKey);
        Assert.AreSame(issues, result.Issues);
        Assert.AreSame(noIssueConclusion, result.NoIssueConclusion);
        Assert.AreSame(verdict, result.Verdict);
        Assert.IsTrue(result.ContinuedAfterVerifierRejectionLimit);
        Assert.IsTrue(result.StoppedAfterMissingSubmissionLimit);
        Assert.AreEqual(2, result.ReviewAttempts);
        Assert.AreEqual(3, result.VerifierAttempts);
        Assert.AreEqual(1, result.RuleReviewAgentResetCount);
    }

    [TestMethod]
    public void RuleReportFactory_PopulatesAllResultFields()
    {
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        StoredRuleReportIssue[] repositoryIssues = [CreateReportIssue()];
        RuleReportDiff diff = new()
        {
            CreatedIssues = repositoryIssues,
            UpdatedIssues = [],
            DeletedIssues = [],
        };
        ReviewVerdict verdict = CreateVerdict(approved: true);

        RuleReportWorkflowResult result = ReportResultFactory.Create(
            "performance",
            taskItem,
            diff,
            repositoryIssues,
            verdict,
            continuedAfterVerifierRejectionLimit: true,
            aggregatorAttempts: 2,
            verifierAttempts: 3);

        Assert.AreEqual("performance", result.RuleKey);
        Assert.AreSame(taskItem, result.TaskItem);
        Assert.AreSame(diff, result.Diff);
        Assert.AreSame(repositoryIssues, result.RepositoryIssues);
        Assert.AreSame(verdict, result.Verdict);
        Assert.IsTrue(result.ContinuedAfterVerifierRejectionLimit);
        Assert.AreEqual(2, result.AggregatorAttempts);
        Assert.AreEqual(3, result.VerifierAttempts);
    }

    private static ReviewVerdict CreateVerdict(bool approved) =>
        new()
        {
            Approved = approved,
            Message = approved ? "Approved." : "Rejected.",
        };

    private static StoredScanProject CreateScanProject() =>
        new()
        {
            ScanProjectId = "scan-1",
            ProjectName = "Core",
            ProjectPath = "CodeSnifferDog",
            ProjectType = "Library",
            Reason = "Contains workflow logic.",
        };

    private static StoredProjectPlanTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-1",
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = "Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static StoredRuleReviewIssue CreateReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue-1",
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

    private static StoredRuleReportIssue CreateReportIssue() =>
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

    private static NoIssueConclusion CreateNoIssueConclusion() =>
        new()
        {
            ReviewStrategy = "Reviewed the target files.",
            ScopeCoverage = "Covered Program.cs.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
            WhyNoIssueWasFound = "The rule is satisfied.",
        };
}
