namespace CodeSnifferDog.Server.Client.Layout.Navigation;

/// <summary>
/// Represents one project row in the sidebar navigation.
/// </summary>
/// <param name="ProjectId">Project identifier shown and used by actions.</param>
/// <param name="Name">Primary project name shown in the sidebar.</param>
/// <param name="Meta">Secondary metadata text shown under the project name.</param>
/// <param name="StatusHref">Agent-status route for this project.</param>
/// <param name="Actions">Actions available for the project row.</param>
internal sealed record ProjectItem(
    string ProjectId,
    string Name,
    string Meta,
    string StatusHref,
    IReadOnlyList<ProjectAction> Actions);
