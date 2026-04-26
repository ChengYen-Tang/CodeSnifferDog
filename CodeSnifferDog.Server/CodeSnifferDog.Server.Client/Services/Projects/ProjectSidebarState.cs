using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects;

public sealed class ProjectSidebarState
{
    public bool IsLoading { get; init; }

    public string? ErrorMessage { get; init; }

    public string? HubErrorMessage { get; init; }

    public IReadOnlyList<ProjectListItemDto> Projects { get; init; } = [];
}
