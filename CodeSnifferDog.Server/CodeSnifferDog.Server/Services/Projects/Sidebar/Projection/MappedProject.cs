using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;

/// <summary>
/// Sidebar projection that pairs raw project data with mapped status and sort timestamps.
/// </summary>
/// <param name="Project">Underlying project projection.</param>
/// <param name="Status">Mapped shared project status.</param>
/// <param name="QueueTimestampUtc">Queue timestamp used for active sorting.</param>
/// <param name="FinishedAtUtc">Finish timestamp used for completed sorting, when one exists.</param>
/// <param name="UpdatedAtUtc">Last update timestamp used as a fallback sort key.</param>
internal sealed record MappedProject(
    ProjectProjection Project,
    ProjectStatus Status,
    DateTimeOffset QueueTimestampUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset UpdatedAtUtc);
