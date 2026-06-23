using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Report;

internal static class ReportToolFactory
{
    public static IList<AITool> CreateAggregatorTools(
        Delegate getRuleReportIssueTool,
        Delegate listRuleReportIssuesTool,
        Delegate createRuleReportIssueTool,
        Delegate updateRuleReportIssueTool,
        Delegate deleteRuleReportIssueTool)
        =>
    [
        AIFunctionFactory.Create(
            getRuleReportIssueTool,
            "GetRuleReportIssue",
            "Get one stored repository-level rule report issue by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            listRuleReportIssuesTool,
            "ListRuleReportIssues",
            "List all repository-level rule report issues for the current rule.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            createRuleReportIssueTool,
            "CreateRuleReportIssue",
            "Create one new repository-level rule report issue for the current rule.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            updateRuleReportIssueTool,
            "UpdateRuleReportIssue",
            "Update one existing repository-level rule report issue by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            deleteRuleReportIssueTool,
            "DeleteRuleReportIssue",
            "Delete one existing repository-level rule report issue by its id.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(Delegate submitReviewVerdictTool)
        =>
    [
        AIFunctionFactory.Create(
            submitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule report diff.",
            serializerOptions: null),
    ];
}
