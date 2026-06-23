using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

internal static class ProjectPlanToolFactory
{
    public static IList<AITool> CreateAgentTools(
        Delegate addProjectPlanTaskItemTool,
        Delegate addProjectPlanTaskItemsTool,
        Delegate deleteProjectPlanTaskItemTool,
        Delegate listProjectPlanTaskItemsTool)
        =>
    [
        AIFunctionFactory.Create(
            addProjectPlanTaskItemTool,
            "AddProjectPlanTaskItem",
            "Add one task item to the current project planning result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            addProjectPlanTaskItemsTool,
            "AddProjectPlanTaskItems",
            "Add multiple task items to the current project planning result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            deleteProjectPlanTaskItemTool,
            "DeleteProjectPlanTaskItem",
            "Delete an existing task item from the current project planning result by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            listProjectPlanTaskItemsTool,
            "ListProjectPlanTaskItems",
            "List all task items currently stored for this project planning attempt.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(
        Delegate listProjectPlanTaskItemsTool,
        Delegate submitReviewVerdictTool)
        =>
    [
        AIFunctionFactory.Create(
            listProjectPlanTaskItemsTool,
            "ListProjectPlanTaskItems",
            "List all task items currently stored for this project planning attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            submitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current project planning result.",
            serializerOptions: null),
    ];
}
