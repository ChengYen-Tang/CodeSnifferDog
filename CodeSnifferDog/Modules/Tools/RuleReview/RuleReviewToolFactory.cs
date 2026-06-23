using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

internal static class RuleReviewToolFactory
{
    public static IList<AITool> CreateAgentTools(
        Delegate createRuleReviewIssueTool,
        Delegate getRuleReviewIssueTool,
        Delegate listRuleReviewIssuesTool,
        Delegate updateRuleReviewIssueTool,
        Delegate deleteRuleReviewIssueTool,
        Delegate submitNoIssueConclusionTool)
        =>
    [
        AIFunctionFactory.Create(
            createRuleReviewIssueTool,
            "CreateRuleReviewIssue",
            "Create one new review issue for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            getRuleReviewIssueTool,
            "GetRuleReviewIssue",
            "Get one stored review issue by its id from the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            listRuleReviewIssuesTool,
            "ListRuleReviewIssues",
            "List all stored review issues for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            updateRuleReviewIssueTool,
            "UpdateRuleReviewIssue",
            "Update one existing review issue by its id for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            deleteRuleReviewIssueTool,
            "DeleteRuleReviewIssue",
            "Delete one existing review issue by its id from the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            submitNoIssueConclusionTool,
            "SubmitNoIssueConclusion",
            "Submit a no-issue conclusion for the current rule review attempt when no issues exist.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(Delegate submitReviewVerdictTool)
        =>
    [
        AIFunctionFactory.Create(
            submitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule review result.",
            serializerOptions: null),
    ];
}
