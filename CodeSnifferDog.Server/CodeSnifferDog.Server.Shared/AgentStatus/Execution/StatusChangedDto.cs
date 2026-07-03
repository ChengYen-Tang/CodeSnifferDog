using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Execution;

public sealed class StatusChangedDto
{
    public required ProjectStatus Status { get; init; }
}
