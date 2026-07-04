namespace CodeSnifferDog.Server.Client.Layout.Navigation;

/// <summary>
/// Represents one action displayed for a project item in the sidebar navigation.
/// </summary>
/// <param name="IconText">Short icon-like text shown for the action.</param>
/// <param name="Label">Accessible label shown for the action.</param>
/// <param name="Kind">Kind of action the UI should execute.</param>
/// <param name="Href">Navigation target for link actions, when applicable.</param>
internal sealed record ProjectAction(string IconText, string Label, ProjectActionKind Kind, string? Href)
{
    /// <summary>
    /// Creates a navigation action.
    /// </summary>
    /// <param name="iconText">Short icon-like text shown for the action.</param>
    /// <param name="label">Accessible label shown for the action.</param>
    /// <param name="href">Navigation target to open.</param>
    /// <returns>The created link action.</returns>
    public static ProjectAction Link(string iconText, string label, string href) =>
        new(iconText, label, ProjectActionKind.Link, href);

    /// <summary>
    /// Creates the delete-project action.
    /// </summary>
    /// <returns>The created delete action.</returns>
    public static ProjectAction Delete() =>
        new("D", "Delete", ProjectActionKind.Delete, null);

    /// <summary>
    /// Creates the cancel-project action.
    /// </summary>
    /// <returns>The created cancel action.</returns>
    public static ProjectAction Cancel() =>
        new("X", "Cancel", ProjectActionKind.Cancel, null);
}
