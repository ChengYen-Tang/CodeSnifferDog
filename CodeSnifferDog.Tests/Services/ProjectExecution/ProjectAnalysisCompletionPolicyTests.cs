using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectAnalysisCompletionPolicyTests
{
    [TestMethod]
    public void Evaluate_ReturnsSuccess_WhenFindingsExistEvenIfReviewStageFailed()
    {
        ReviewAgentTeamAnalysisCompletionDecision decision = ReviewAgentTeamAnalysisCompletionPolicy.Evaluate(new ReviewAgentTeamAnalysisResult
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = false,
            HasAnyFindings = true,
            AllRuleFlowsSucceeded = false,
            ExecutionErrors = ["rule-b flow failed."],
            RuleReports = [CreateRuleReport("rule-a")],
        });

        Assert.IsTrue(decision.IsSuccess);
        Assert.IsTrue(decision.ShouldPersistReports);
        Assert.IsNull(decision.FailureMessage);
    }

    [TestMethod]
    public void Evaluate_ReturnsSuccess_WhenNoFindingsAndAllRuleFlowsSucceeded()
    {
        ReviewAgentTeamAnalysisCompletionDecision decision = ReviewAgentTeamAnalysisCompletionPolicy.Evaluate(new ReviewAgentTeamAnalysisResult
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = true,
            HasAnyFindings = false,
            AllRuleFlowsSucceeded = true,
            ExecutionErrors = [],
            RuleReports = [CreateRuleReport("rule-a")],
        });

        Assert.IsTrue(decision.IsSuccess);
        Assert.IsTrue(decision.ShouldPersistReports);
        Assert.IsNull(decision.FailureMessage);
    }

    [TestMethod]
    public void Evaluate_ReturnsFailure_WhenNoFindingsAndReviewStageFailed()
    {
        ReviewAgentTeamAnalysisCompletionDecision decision = ReviewAgentTeamAnalysisCompletionPolicy.Evaluate(new ReviewAgentTeamAnalysisResult
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = false,
            HasAnyFindings = false,
            AllRuleFlowsSucceeded = false,
            ExecutionErrors = ["rule-b flow failed."],
            RuleReports = [CreateRuleReport("rule-a")],
        });

        Assert.IsFalse(decision.IsSuccess);
        Assert.IsFalse(decision.ShouldPersistReports);
        Assert.IsNotNull(decision.FailureMessage);
        StringAssert.Contains(decision.FailureMessage, "rule-b flow failed.");
    }

    [TestMethod]
    public void Evaluate_ReturnsFailure_WhenNoFindingsAndSomeRuleFlowsDegraded()
    {
        ReviewAgentTeamAnalysisCompletionDecision decision = ReviewAgentTeamAnalysisCompletionPolicy.Evaluate(new ReviewAgentTeamAnalysisResult
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = true,
            HasAnyFindings = false,
            AllRuleFlowsSucceeded = false,
            ExecutionErrors = [],
            RuleReports = [CreateRuleReport("rule-a")],
        });

        Assert.IsFalse(decision.IsSuccess);
        Assert.IsFalse(decision.ShouldPersistReports);
        Assert.IsNotNull(decision.FailureMessage);
        StringAssert.Contains(decision.FailureMessage, "did not finish successfully");
    }

    private static ReviewAgentTeamRuleReport CreateRuleReport(string ruleKey) =>
        new()
        {
            RuleKey = ruleKey,
            MarkdownContent = $"# {ruleKey}-report.md",
        };
}
