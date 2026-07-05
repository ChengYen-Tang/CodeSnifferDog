using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using ProjectPlanToolSet = CodeSnifferDog.Modules.Tools.ProjectPlan.ToolSet;
using RuleReviewToolSet = CodeSnifferDog.Modules.Tools.RuleReview.ToolSet;
using ReportToolSet = CodeSnifferDog.Modules.Tools.Report.ToolSet;
using RuleReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.InMemoryIssueStore;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.InMemoryIssueStore;

namespace CodeSnifferDog.Tests.Modules.Tools;

[TestClass]
public sealed class ToolMetadataCompatibilityTests
{
    [TestMethod]
    public void CommonTools_PreserveToolNamesAndDescriptions()
    {
        CommonToolSet toolSet = new(Environment.CurrentDirectory);

        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateTools(),
            [
                ("Shell", "Run one shell command in the repository root path. Use PowerShell on Windows and bash on Linux/macOS. Pass only the command text to execute."),
                ("Ripgrep", "Run one ripgrep search command in the repository root path. Pass only the arguments after rg. Do not include rg in the command text. Example: use \"-n \\\"SystemPrompt\\\" .\" instead of \"rg -n \\\"SystemPrompt\\\" .\"."),
            ]);
    }

    [TestMethod]
    public void ScanTools_PreserveToolNamesAndDescriptions()
    {
        ScanToolSet toolSet = new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateScanAgentTools(),
            [
                ("AddScanProject", "Add one discovered project unit to the current scan result."),
                ("AddScanProjects", "Add multiple discovered project units to the current scan result."),
                ("DeleteScanProject", "Delete an existing scan project from the current scan result by its id."),
                ("ListScanProjects", "List all scan projects currently stored for this scan attempt."),
            ]);
        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateVerifierTools(),
            [
                ("ListScanProjects", "List all scan projects currently stored for this scan attempt."),
                ("SubmitReviewVerdict", "Submit the verifier approval or rejection for the current scan result."),
            ]);
    }

    [TestMethod]
    public void ProjectPlanTools_PreserveToolNamesAndDescriptions()
    {
        ProjectPlanToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateProjectPlanAgentTools(),
            [
                ("AddProjectPlanTaskItem", "Add one task item to the current project planning result."),
                ("AddProjectPlanTaskItems", "Add multiple task items to the current project planning result."),
                ("DeleteProjectPlanTaskItem", "Delete an existing task item from the current project planning result by its id."),
                ("ListProjectPlanTaskItems", "List all task items currently stored for this project planning attempt."),
            ]);
        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateVerifierTools(),
            [
                ("ListProjectPlanTaskItems", "List all task items currently stored for this project planning attempt."),
                ("SubmitReviewVerdict", "Submit the verifier approval or rejection for the current project planning result."),
            ]);
    }

    [TestMethod]
    public void RuleReviewTools_PreserveToolNamesAndDescriptions()
    {
        RuleReviewToolSet toolSet = new(
            new RuleReviewIssueStore(),
            new ReviewVerdictBuffer(),
            TestKeys.RuleFlowKey);

        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateRuleReviewAgentTools(),
            [
                ("CreateRuleReviewIssue", "Create one new review issue for the current rule review attempt."),
                ("GetRuleReviewIssue", "Get one stored review issue by its id from the current rule review attempt."),
                ("ListRuleReviewIssues", "List all stored review issues for the current rule review attempt."),
                ("UpdateRuleReviewIssue", "Update one existing review issue by its id for the current rule review attempt."),
                ("DeleteRuleReviewIssue", "Delete one existing review issue by its id from the current rule review attempt."),
                ("SubmitNoIssueConclusion", "Submit a no-issue conclusion for the current rule review attempt when no issues exist."),
            ]);
        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateVerifierTools(),
            [
                ("SubmitReviewVerdict", "Submit the verifier approval or rejection for the current rule review result."),
            ]);
    }

    [TestMethod]
    public void ReportTools_PreserveToolNamesAndDescriptions()
    {
        ReportToolSet toolSet = new(
            new ReportIssueStore(),
            new ReviewVerdictBuffer(),
            TestKeys.RuleFlowKey,
            TestKeys.RuleReportKey);

        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateReportAggregatorTools(),
            [
                ("GetRuleReportIssue", "Get one stored repository-level rule report issue by its id."),
                ("ListRuleReportIssues", "List all repository-level rule report issues for the current rule."),
                ("CreateRuleReportIssue", "Create one new repository-level rule report issue for the current rule."),
                ("UpdateRuleReportIssue", "Update one existing repository-level rule report issue by its id."),
                ("DeleteRuleReportIssue", "Delete one existing repository-level rule report issue by its id."),
            ]);
        ToolMetadataAssertions.AssertToolMetadata(
            toolSet.CreateVerifierTools(),
            [
                ("SubmitReviewVerdict", "Submit the verifier approval or rejection for the current rule report diff."),
            ]);
    }

    [TestMethod]
    public void ScanAdapters_PreserveParameterDescriptions()
    {
        ToolMetadataAssertions.AssertAdapterDescription<ScanToolSet>(
            "AddScanProjectToolAsync",
            "Add one discovered project unit to the current scan result.",
            new Dictionary<string, string>
            {
                ["ProjectName"] = "The display name of the discovered project unit.",
                ["ProjectPath"] = "The repository-relative path or canonical path that identifies the discovered project unit.",
                ["ProjectType"] = "The project category or file type, such as .csproj, package.json, or directory-based module.",
                ["Reason"] = "Why this project unit should enter the next planning stage.",
            });
        ToolMetadataAssertions.AssertAdapterDescription<ScanToolSet>(
            "AddScanProjectsToolAsync",
            "Add multiple discovered project units to the current scan result.",
            new Dictionary<string, string>
            {
                ["Projects"] = "The project units to add to the current scan result.",
            });
        ToolMetadataAssertions.AssertAdapterDescription<ScanToolSet>(
            "DeleteScanProjectToolAsync",
            "Delete one existing scan project from the current scan result by its id.",
            new Dictionary<string, string>
            {
                ["ScanProjectId"] = "The id of the stored scan project to delete from the current scan result.",
            });
        AssertVerdictAdapter<ScanToolSet>(
            "Submit the verifier approval or rejection for the current scan result.",
            "True when the current scan result is approved. False when more work is required.",
            "The approval note or the rejection reason that explains what the scan agent should keep or fix.");
    }

    [TestMethod]
    public void CommonAdapters_PreserveParameterDescriptions()
    {
        ToolMetadataAssertions.AssertAdapterDescription<CommonToolSet>(
            "RunShellCommandToolAsync",
            "Run one shell command in the repository root path. Use PowerShell on Windows and bash on Linux or macOS.",
            new Dictionary<string, string>
            {
                ["Command"] = "The shell command text to execute inside the repository root path.",
            });
        ToolMetadataAssertions.AssertAdapterDescription<CommonToolSet>(
            "RunRipgrepCommandToolAsync",
            "Run one ripgrep search command in the repository root path.",
            new Dictionary<string, string>
            {
                ["Command"] = "Arguments after rg. Do not include rg or rg.exe. Example: use \"-n \\\"SystemPrompt\\\" .\" instead of \"rg -n \\\"SystemPrompt\\\" .\". Full paths are allowed when you need to inspect files outside the repository root path.",
            });
    }

    [TestMethod]
    public void ProjectPlanAdapters_PreserveParameterDescriptions()
    {
        ToolMetadataAssertions.AssertAdapterDescription<ProjectPlanToolSet>(
            "AddProjectPlanTaskItemToolAsync",
            "Add one task item to the current project planning result.",
            new Dictionary<string, string>
            {
                ["Files"] = "The scope entry files that belong to this task item. Must be a JSON array of objects. Each object must include filePath and totalLines. totalLines must be a positive integer greater than 0; count or estimate it from the file content and never use 0 for unknown. Example: [{\"filePath\":\"src/Foo.cs\",\"totalLines\":120}].",
            });
        ToolMetadataAssertions.AssertAdapterDescription<ProjectPlanToolSet>(
            "AddProjectPlanTaskItemsToolAsync",
            "Add multiple task items to the current project planning result.",
            new Dictionary<string, string>
            {
                ["TaskItems"] = "The task items to add to the current project planning result. Must be a JSON array of task item objects. Each task item must include Files, and Files must be an array of objects with filePath and totalLines. totalLines must be a positive integer greater than 0; count or estimate it from the file content and never use 0 for unknown. Example: [{\"Files\":[{\"filePath\":\"src/Foo.cs\",\"totalLines\":120}]}].",
            });
        ToolMetadataAssertions.AssertAdapterDescription<ProjectPlanToolSet>(
            "DeleteProjectPlanTaskItemToolAsync",
            "Delete one existing task item from the current project planning result by its id.",
            new Dictionary<string, string>
            {
                ["ProjectPlanTaskItemId"] = "The id of the stored task item to delete from the current project planning result.",
            });
        AssertVerdictAdapter<ProjectPlanToolSet>(
            "Submit the verifier approval or rejection for the current project planning result.",
            "True when the current project planning result is approved. False when more work is required.",
            "The approval note or the rejection reason that explains what the planner should keep or fix.");
    }

    [TestMethod]
    public void RuleReviewAdapters_PreserveParameterDescriptions()
    {
        ToolMetadataAssertions.AssertAdapterDescription<RuleReviewToolSet>(
            "CreateRuleReviewIssueToolAsync",
            "Create one new review issue for the current rule review attempt.",
            IssueParameters(
                issueType: "The issue type for the discovered problem.",
                severity: "The severity level for the discovered problem. Allowed values: High, Medium, Low.",
                fileOrFunction: "The related file or function for the discovered problem.",
                relevantCodePatternOrExpression: "The relevant code pattern or expression that supports the issue.",
                whyThisIsAProblem: "Why this is a problem under the current rule.",
                confidence: "The confidence level for this issue, typically High, Medium, or Low.",
                followUpFiles: "Any follow-up files that should be referenced for this issue.",
                suggestedFixDirection: "The suggested fix direction for this issue.",
                scopeCoverage: "What scope entry files were inspected, what was skipped, why, and whether coverage is sufficient.",
                crossScopeAnalysis: "What cross-scope analysis was performed, which follow-up files were inspected, and why.",
                reviewStrategy: "The review strategy used to discover and validate this issue."));
        ToolMetadataAssertions.AssertAdapterDescription<RuleReviewToolSet>(
            "GetRuleReviewIssueToolAsync",
            "Get one stored review issue by its id from the current rule review attempt.",
            new Dictionary<string, string>
            {
                ["RuleReviewIssueId"] = "The id of the stored review issue to retrieve.",
            });
        ToolMetadataAssertions.AssertAdapterDescription<RuleReviewToolSet>(
            "UpdateRuleReviewIssueToolAsync",
            "Update one existing review issue by its id for the current rule review attempt.",
            UpdatedIssueParameters("RuleReviewIssueId", "The id of the stored review issue to update."));
        ToolMetadataAssertions.AssertAdapterDescription<RuleReviewToolSet>(
            "DeleteRuleReviewIssueToolAsync",
            "Delete one existing review issue by its id from the current rule review attempt.",
            new Dictionary<string, string>
            {
                ["RuleReviewIssueId"] = "The id of the stored review issue to delete.",
            });
        ToolMetadataAssertions.AssertAdapterDescription<RuleReviewToolSet>(
            "SubmitNoIssueConclusionToolAsync",
            "Submit a no-issue conclusion for the current rule review attempt when no issues exist.",
            new Dictionary<string, string>
            {
                ["ReviewStrategy"] = "The review strategy used before concluding that no issue exists.",
                ["ScopeCoverage"] = "What scope entry files were inspected, what was skipped, why, and whether coverage is sufficient.",
                ["CrossScopeAnalysis"] = "What cross-scope analysis was performed, which follow-up files were inspected, and why.",
                ["WhyNoIssueWasFound"] = "Why no issue was found under the current rule.",
            });
        AssertVerdictAdapter<RuleReviewToolSet>(
            "Submit the verifier approval or rejection for the current rule review result.",
            "True when the current rule review result is approved. False when more work is required.",
            "The approval note or the rejection reason that explains what the reviewer should keep or fix.");
    }

    [TestMethod]
    public void ReportAdapters_PreserveParameterDescriptions()
    {
        ToolMetadataAssertions.AssertAdapterDescription<ReportToolSet>(
            "GetRuleReportIssueToolAsync",
            "Get one stored repository-level rule report issue by its id.",
            new Dictionary<string, string>
            {
                ["RuleReportIssueId"] = "The id of the stored repository-level rule report issue to retrieve.",
            });
        ToolMetadataAssertions.AssertAdapterDescription<ReportToolSet>(
            "CreateRuleReportIssueToolAsync",
            "Create one new repository-level rule report issue for the current rule.",
            IssueParameters(
                issueType: "The issue type for the repository-level issue.",
                severity: "The severity level for the repository-level issue. Allowed values: High, Medium, Low.",
                fileOrFunction: "The related file or function for the repository-level issue.",
                relevantCodePatternOrExpression: "The relevant code pattern or expression for the repository-level issue.",
                whyThisIsAProblem: "Why this is a problem for the repository-level issue.",
                confidence: "The confidence level for this repository-level issue.",
                followUpFiles: "Any follow-up files that support this repository-level issue.",
                suggestedFixDirection: "The suggested fix direction for this repository-level issue.",
                scopeCoverage: "The scope coverage explanation for this repository-level issue.",
                crossScopeAnalysis: "The cross-scope analysis explanation for this repository-level issue.",
                reviewStrategy: "The review strategy for this repository-level issue."));
        ToolMetadataAssertions.AssertAdapterDescription<ReportToolSet>(
            "UpdateRuleReportIssueToolAsync",
            "Update one existing repository-level rule report issue by its id.",
            UpdatedIssueParameters("RuleReportIssueId", "The id of the stored repository-level rule report issue to update."));
        ToolMetadataAssertions.AssertAdapterDescription<ReportToolSet>(
            "DeleteRuleReportIssueToolAsync",
            "Delete one existing repository-level rule report issue by its id.",
            new Dictionary<string, string>
            {
                ["RuleReportIssueId"] = "The id of the stored repository-level rule report issue to delete.",
            });
        AssertVerdictAdapter<ReportToolSet>(
            "Submit the verifier approval or rejection for the current rule report diff.",
            "True when the current rule report diff is approved. False when more work is required.",
            "The approval note or the rejection reason that explains what the aggregator should keep or fix.");
    }

    private static void AssertVerdictAdapter<TToolSet>(
        string methodDescription,
        string approvedDescription,
        string messageDescription) =>
        ToolMetadataAssertions.AssertAdapterDescription<TToolSet>(
            "SubmitReviewVerdictToolAsync",
            methodDescription,
            new Dictionary<string, string>
            {
                ["Approved"] = approvedDescription,
                ["Message"] = messageDescription,
            });

    private static Dictionary<string, string> IssueParameters(
        string issueType,
        string severity,
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string whyThisIsAProblem,
        string confidence,
        string followUpFiles,
        string suggestedFixDirection,
        string scopeCoverage,
        string crossScopeAnalysis,
        string reviewStrategy) =>
        new()
        {
            ["IssueType"] = issueType,
            ["Severity"] = severity,
            ["FileOrFunction"] = fileOrFunction,
            ["RelevantCodePatternOrExpression"] = relevantCodePatternOrExpression,
            ["WhyThisIsAProblem"] = whyThisIsAProblem,
            ["Confidence"] = confidence,
            ["FollowUpFiles"] = followUpFiles,
            ["SuggestedFixDirection"] = suggestedFixDirection,
            ["ScopeCoverage"] = scopeCoverage,
            ["CrossScopeAnalysis"] = crossScopeAnalysis,
            ["ReviewStrategy"] = reviewStrategy,
        };

    private static Dictionary<string, string> UpdatedIssueParameters(string idParameterName, string idParameterDescription)
    {
        Dictionary<string, string> parameters = IssueParameters(
            issueType: "The updated issue type.",
            severity: "The updated severity level. Allowed values: High, Medium, Low.",
            fileOrFunction: "The updated related file or function.",
            relevantCodePatternOrExpression: "The updated relevant code pattern or expression.",
            whyThisIsAProblem: "The updated explanation of why this is a problem.",
            confidence: "The updated confidence level.",
            followUpFiles: "The updated follow-up files.",
            suggestedFixDirection: "The updated suggested fix direction.",
            scopeCoverage: "The updated scope coverage explanation.",
            crossScopeAnalysis: "The updated cross-scope analysis explanation.",
            reviewStrategy: "The updated review strategy.");
        parameters[idParameterName] = idParameterDescription;
        return parameters;
    }
}

file static class TestKeys
{
    public static readonly CodeSnifferDog.Models.Review.RuleFlowKey RuleFlowKey =
        CodeSnifferDog.Models.Review.RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", "performance");

    public static readonly CodeSnifferDog.Models.Review.RuleReportKey RuleReportKey =
        CodeSnifferDog.Models.Review.RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", "performance");
}
