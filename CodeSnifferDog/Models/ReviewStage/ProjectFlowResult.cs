namespace CodeSnifferDog.Models.ReviewStage;

/// <summary>
/// Holds the task-item flow results produced for one project.
/// </summary>
public sealed class ProjectFlowResult
{
    /// <summary>
    /// Gets the task-item flow results for the project.
    /// </summary>
    public required IReadOnlyList<TaskItemFlowResult> TaskItemResults { get; init; }
}
