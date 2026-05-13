using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectExecutionStatusChangedDto
{
    public required ProjectStatus Status { get; init; }
}
