using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

/// <summary>
/// Creates the AI tools exposed to project-plan agents and verifiers.
/// </summary>
internal static class ToolFactory
{
    /// <summary>
    /// Creates the tools used by project-plan agents.
    /// </summary>
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

    /// <summary>
    /// Creates the tools used by project-plan verifiers.
    /// </summary>
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

/// <summary>
/// Groups callbacks used by project-plan agent tools.
/// </summary>
internal readonly record struct ProjectPlanAgentToolCallbacks(
    AddProjectPlanTaskItemToolCallback AddProjectPlanTaskItemTool,
    AddProjectPlanTaskItemsToolCallback AddProjectPlanTaskItemsTool,
    DeleteProjectPlanTaskItemToolCallback DeleteProjectPlanTaskItemTool,
    ListProjectPlanTaskItemsToolCallback ListProjectPlanTaskItemsTool);

/// <summary>
/// Groups callbacks used by project-plan verifier tools.
/// </summary>
internal readonly record struct ProjectPlanVerifierToolCallbacks(
    ListProjectPlanTaskItemsToolCallback ListProjectPlanTaskItemsTool,
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

/// <summary>
/// Represents the callback used to add one task item.
/// </summary>
internal delegate ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemToolCallback(
    IReadOnlyList<PlanFile> Files,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to add multiple task items.
/// </summary>
internal delegate ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsToolCallback(
    IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to delete one stored task item.
/// </summary>
internal delegate ValueTask<bool> DeleteProjectPlanTaskItemToolCallback(
    string ProjectPlanTaskItemId,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to list stored task items.
/// </summary>
internal delegate ValueTask<IReadOnlyList<StoredTaskItem>> ListProjectPlanTaskItemsToolCallback(
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to submit the verifier verdict.
/// </summary>
internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
