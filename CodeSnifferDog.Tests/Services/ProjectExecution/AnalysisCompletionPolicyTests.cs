using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AnalysisCompletionPolicyTests
{
    [TestMethod]
    public void Evaluate_ReturnsSuccess_WhenFindingsExistEvenIfReviewStageFailed()
    {
        CompletionDecision decision = CompletionPolicy.Evaluate(new AnalysisResult
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
        CompletionDecision decision = CompletionPolicy.Evaluate(new AnalysisResult
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
        CompletionDecision decision = CompletionPolicy.Evaluate(new AnalysisResult
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
        CompletionDecision decision = CompletionPolicy.Evaluate(new AnalysisResult
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

    private static RuleReport CreateRuleReport(string ruleKey) =>
        new()
        {
            RuleKey = ruleKey,
            MarkdownContent = $"# {ruleKey}-report.md",
        };
}
