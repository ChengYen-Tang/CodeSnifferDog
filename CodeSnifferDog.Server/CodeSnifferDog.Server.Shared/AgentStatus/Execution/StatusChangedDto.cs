using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Execution;

/// <summary>
/// Represents a project execution status change.
/// </summary>
public sealed class StatusChangedDto
{
    /// <summary>
    /// Gets the current project status.
    /// </summary>
    public required ProjectStatus Status { get; init; }
}
