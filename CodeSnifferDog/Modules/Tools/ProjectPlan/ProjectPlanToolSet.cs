using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public sealed class ProjectPlanToolSet
{
    private readonly ProjectPlanTaskItemToolService _taskItemToolService;
    private readonly ReviewVerdictToolService _verdictToolService;

    public ProjectPlanToolSet(IProjectPlanTaskItemStore taskItemStore, ReviewVerdictBuffer verdictBuffer)
        : this(new ProjectPlanTaskItemToolService(taskItemStore), new ReviewVerdictToolService(verdictBuffer))
    {
    }

    internal ProjectPlanToolSet(
        ProjectPlanTaskItemToolService taskItemToolService,
        ReviewVerdictToolService verdictToolService)
    {
        _taskItemToolService = taskItemToolService;
        _verdictToolService = verdictToolService;
    }

    public IList<AITool> CreateProjectPlanAgentTools()
        =>
        ProjectPlanToolFactory.CreateAgentTools(new ProjectPlanAgentToolCallbacks(
            AddProjectPlanTaskItemToolAsync,
            AddProjectPlanTaskItemsToolAsync,
            DeleteProjectPlanTaskItemToolAsync,
            ListProjectPlanTaskItemsAsync));

    public IList<AITool> CreateVerifierTools()
        =>
        ProjectPlanToolFactory.CreateVerifierTools(new ProjectPlanVerifierToolCallbacks(
            ListProjectPlanTaskItemsAsync,
            SubmitReviewVerdictToolAsync));

    [Description("Add one task item to the current project planning result.")]
    private ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemToolAsync(
        [Description("The scope entry files that belong to this task item.")]
        IReadOnlyList<ProjectPlanFile> Files,
        CancellationToken cancellationToken) =>
        AddProjectPlanTaskItemAsync(
            new AddProjectPlanTaskItemArgs
            {
                Files = Files,
            },
            cancellationToken);

    [Description("Add multiple task items to the current project planning result.")]
    private ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsToolAsync(
        [Description("The task items to add to the current project planning result.")]
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

    public ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemAsync(
        AddProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken) =>
        _taskItemToolService.AddProjectPlanTaskItemAsync(args, cancellationToken);

    public ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsAsync(
        AddProjectPlanTaskItemsArgs args,
        CancellationToken cancellationToken) =>
        _taskItemToolService.AddProjectPlanTaskItemsAsync(args, cancellationToken);

    public ValueTask<bool> DeleteProjectPlanTaskItemAsync(
        DeleteProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken) =>
        _taskItemToolService.DeleteProjectPlanTaskItemAsync(args, cancellationToken);

    public ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListProjectPlanTaskItemsAsync(CancellationToken cancellationToken)
        =>
        _taskItemToolService.ListProjectPlanTaskItemsAsync(cancellationToken);

    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _) =>
        _verdictToolService.SubmitReviewVerdictAsync(args);
}
