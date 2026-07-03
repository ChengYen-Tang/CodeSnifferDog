using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

internal static class ToolFactory
{
    public static IList<AITool> CreateAgentTools(ProjectPlanAgentToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.AddProjectPlanTaskItemTool,
            "AddProjectPlanTaskItem",
            "Add one task item to the current project planning result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.AddProjectPlanTaskItemsTool,
            "AddProjectPlanTaskItems",
            "Add multiple task items to the current project planning result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.DeleteProjectPlanTaskItemTool,
            "DeleteProjectPlanTaskItem",
            "Delete an existing task item from the current project planning result by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.ListProjectPlanTaskItemsTool,
            "ListProjectPlanTaskItems",
            "List all task items currently stored for this project planning attempt.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(ProjectPlanVerifierToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.ListProjectPlanTaskItemsTool,
            "ListProjectPlanTaskItems",
            "List all task items currently stored for this project planning attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.SubmitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current project planning result.",
            serializerOptions: null),
    ];
}

internal readonly record struct ProjectPlanAgentToolCallbacks(
    AddProjectPlanTaskItemToolCallback AddProjectPlanTaskItemTool,
    AddProjectPlanTaskItemsToolCallback AddProjectPlanTaskItemsTool,
    DeleteProjectPlanTaskItemToolCallback DeleteProjectPlanTaskItemTool,
    ListProjectPlanTaskItemsToolCallback ListProjectPlanTaskItemsTool);

internal readonly record struct ProjectPlanVerifierToolCallbacks(
    ListProjectPlanTaskItemsToolCallback ListProjectPlanTaskItemsTool,
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

internal delegate ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemToolCallback(
    IReadOnlyList<PlanFile> Files,
    CancellationToken cancellationToken);

internal delegate ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsToolCallback(
    IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems,
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> DeleteProjectPlanTaskItemToolCallback(
    string ProjectPlanTaskItemId,
    CancellationToken cancellationToken);

internal delegate ValueTask<IReadOnlyList<StoredTaskItem>> ListProjectPlanTaskItemsToolCallback(
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
