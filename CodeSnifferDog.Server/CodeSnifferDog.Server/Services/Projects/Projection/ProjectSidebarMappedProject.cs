using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal sealed record ProjectSidebarMappedProject(
    ProjectSidebarProjectProjection Project,
    ProjectStatus Status,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset UpdatedAtUtc);
