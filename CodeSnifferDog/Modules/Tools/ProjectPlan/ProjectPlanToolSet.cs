using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public sealed class ProjectPlanToolSet(IProjectPlanTaskItemStore taskItemStore, ReviewVerdictBuffer verdictBuffer)
{
    private readonly IProjectPlanTaskItemStore _taskItemStore = taskItemStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;

    public IList<AITool> CreateProjectPlanAgentTools()
        =>
    [
        AIFunctionFactory.Create(
            AddProjectPlanTaskItemToolAsync,
            "AddProjectPlanTaskItem",
            "Add one task item to the current project planning result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            AddProjectPlanTaskItemsToolAsync,
            "AddProjectPlanTaskItems",
            "Add multiple task items to the current project planning result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            DeleteProjectPlanTaskItemToolAsync,
            "DeleteProjectPlanTaskItem",
            "Delete an existing task item from the current project planning result by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            ListProjectPlanTaskItemsAsync,
            "ListProjectPlanTaskItems",
            "List all task items currently stored for this project planning attempt.",
            serializerOptions: null),
    ];

    public IList<AITool> CreateVerifierTools()
        =>
    [
        AIFunctionFactory.Create(
            ListProjectPlanTaskItemsAsync,
            "ListProjectPlanTaskItems",
            "List all task items currently stored for this project planning attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            SubmitReviewVerdictToolAsync,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current project planning result.",
            serializerOptions: null),
    ];

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

    public async ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemAsync(
        AddProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ValidateFiles(args.Files, nameof(args));

        StoredProjectPlanTaskItem storedTaskItem = await _taskItemStore.AddAsync(
            new ProjectPlanTaskItem
            {
                Files = [.. args.Files.Select(CreateNormalizedFile)],
            },
            cancellationToken).ConfigureAwait(false);

        return new AddProjectPlanTaskItemResult
        {
            ProjectPlanTaskItemId = storedTaskItem.ProjectPlanTaskItemId,
        };
    }

    public async ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsAsync(
        AddProjectPlanTaskItemsArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.TaskItems.Count == 0)
            throw new ArgumentException("At least one project plan task item is required.", nameof(args));

        foreach (AddProjectPlanTaskItemArgs taskItem in args.TaskItems)
            ValidateFiles(taskItem.Files, nameof(args));

        IReadOnlyList<StoredProjectPlanTaskItem> storedTaskItems = await _taskItemStore.AddRangeAsync(
            [.. args.TaskItems.Select(taskItem => new ProjectPlanTaskItem
            {
                Files = [.. taskItem.Files.Select(CreateNormalizedFile)],
            })],
            cancellationToken).ConfigureAwait(false);

        return new AddProjectPlanTaskItemsResult
        {
            ProjectPlanTaskItemIds = [.. storedTaskItems.Select(taskItem => taskItem.ProjectPlanTaskItemId)],
        };
    }

    public ValueTask<bool> DeleteProjectPlanTaskItemAsync(
        DeleteProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPlanTaskItemId);
        return _taskItemStore.DeleteAsync(args.ProjectPlanTaskItemId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListProjectPlanTaskItemsAsync(CancellationToken cancellationToken)
        =>
        _taskItemStore.ListAsync(cancellationToken);

    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }

    private static ProjectPlanFile CreateNormalizedFile(ProjectPlanFile file)
        =>
        new()
        {
            FilePath = file.FilePath.Trim(),
            TotalLines = file.TotalLines,
        };

    private static void ValidateFiles(IReadOnlyList<ProjectPlanFile> files, string paramName)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
            throw new ArgumentException("At least one project plan file is required.", paramName);

        foreach (ProjectPlanFile file in files)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrWhiteSpace(file.FilePath);

            if (file.TotalLines <= 0)
                throw new ArgumentOutOfRangeException(paramName, "Total lines must be greater than zero.");
        }
    }
}
