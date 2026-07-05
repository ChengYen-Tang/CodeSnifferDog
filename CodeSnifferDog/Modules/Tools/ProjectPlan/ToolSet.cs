using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

/// <summary>
/// Builds the tool set used by project-plan agents and verifiers.
/// </summary>
public sealed class ToolSet
{
    private readonly TaskItemToolService _taskItemToolService;
    private readonly ReviewVerdictToolService _verdictToolService;

    public ToolSet(ITaskItemStore taskItemStore, ReviewVerdictBuffer verdictBuffer)
        : this(new TaskItemToolService(taskItemStore), new ReviewVerdictToolService(verdictBuffer))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolSet"/> class for tests or composed services.
    /// </summary>
    internal ToolSet(
        TaskItemToolService taskItemToolService,
        ReviewVerdictToolService verdictToolService)
    {
        _taskItemToolService = taskItemToolService;
        _verdictToolService = verdictToolService;
    }

    /// <summary>
    /// Creates the tools used by project-plan agents.
    /// </summary>
    public IList<AITool> CreateProjectPlanAgentTools()
        =>
        ToolFactory.CreateAgentTools(new ProjectPlanAgentToolCallbacks(
            AddProjectPlanTaskItemToolAsync,
            AddProjectPlanTaskItemsToolAsync,
            DeleteProjectPlanTaskItemToolAsync,
            ListProjectPlanTaskItemsAsync));

    /// <summary>
    /// Creates the tools used by project-plan verifiers.
    /// </summary>
    public IList<AITool> CreateVerifierTools()
        =>
        ToolFactory.CreateVerifierTools(new ProjectPlanVerifierToolCallbacks(
            ListProjectPlanTaskItemsAsync,
            SubmitReviewVerdictToolAsync));

    [Description("Add one task item to the current project planning result.")]
    private ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemToolAsync(
        [Description("The scope entry files that belong to this task item. Must be a JSON array of objects. Each object must include filePath and totalLines. Example: [{\"filePath\":\"src/Foo.cs\",\"totalLines\":120}].")]
        IReadOnlyList<PlanFile> Files,
        CancellationToken cancellationToken) =>
        AddProjectPlanTaskItemAsync(
            new AddProjectPlanTaskItemArgs
            {
                Files = Files,
            },
            cancellationToken);

    [Description("Add multiple task items to the current project planning result.")]
    private ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsToolAsync(
        [Description("The task items to add to the current project planning result. Must be a JSON array of task item objects. Each task item must include Files, and Files must be an array of objects with filePath and totalLines. Example: [{\"Files\":[{\"filePath\":\"src/Foo.cs\",\"totalLines\":120}]}].")]
        IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems,
        CancellationToken cancellationToken) =>
        AddProjectPlanTaskItemsAsync(
            new AddProjectPlanTaskItemsArgs
            {
                TaskItems = TaskItems,
            },
            cancellationToken);

    [Description("Delete one existing task item from the current project planning result by its id.")]
    private ValueTask<bool> DeleteProjectPlanTaskItemToolAsync(
        [Description("The id of the stored task item to delete from the current project planning result.")]
        string ProjectPlanTaskItemId,
        CancellationToken cancellationToken) =>
        DeleteProjectPlanTaskItemAsync(
            new DeleteProjectPlanTaskItemArgs
            {
                ProjectPlanTaskItemId = ProjectPlanTaskItemId,
            },
            cancellationToken);

    [Description("Submit the verifier approval or rejection for the current project planning result.")]
    private ValueTask<bool> SubmitReviewVerdictToolAsync(
        [Description("True when the current project planning result is approved. False when more work is required.")]
        bool Approved,
        [Description("The approval note or the rejection reason that explains what the planner should keep or fix.")]
        string Message,
        CancellationToken cancellationToken) =>
        SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = Approved,
                Message = Message,
            },
            cancellationToken);

    /// <summary>
    /// Adds one project-plan task item.
    /// </summary>
    public ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemAsync(
        AddProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken) =>
        _taskItemToolService.AddProjectPlanTaskItemAsync(args, cancellationToken);

    /// <summary>
    /// Adds multiple project-plan task items.
    /// </summary>
    public ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsAsync(
        AddProjectPlanTaskItemsArgs args,
        CancellationToken cancellationToken) =>
        _taskItemToolService.AddProjectPlanTaskItemsAsync(args, cancellationToken);

    /// <summary>
    /// Deletes one stored project-plan task item.
    /// </summary>
    public ValueTask<bool> DeleteProjectPlanTaskItemAsync(
        DeleteProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken) =>
        _taskItemToolService.DeleteProjectPlanTaskItemAsync(args, cancellationToken);

    /// <summary>
    /// Lists all stored project-plan task items.
    /// </summary>
    public ValueTask<IReadOnlyList<StoredTaskItem>> ListProjectPlanTaskItemsAsync(CancellationToken cancellationToken)
        =>
        _taskItemToolService.ListProjectPlanTaskItemsAsync(cancellationToken);

    /// <summary>
    /// Stores the verifier verdict for the current project-plan attempt.
    /// </summary>
    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _) =>
        _verdictToolService.SubmitReviewVerdictAsync(args);
}
