using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;

internal sealed record MappedProject(
    ProjectProjection Project,
    ProjectStatus Status,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset UpdatedAtUtc);
