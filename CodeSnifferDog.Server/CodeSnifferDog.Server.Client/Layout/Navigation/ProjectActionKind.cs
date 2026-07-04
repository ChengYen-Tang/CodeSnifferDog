namespace CodeSnifferDog.Server.Client.Layout.Navigation;

/// <summary>
/// Identifies how the sidebar should execute one project action.
/// </summary>
internal enum ProjectActionKind
{
    /// <summary>
    /// Opens a navigation target.
    /// </summary>
    Link,

    /// <summary>
    /// Requests cancellation of a running project.
    /// </summary>
    Cancel,

    /// <summary>
    /// Requests deletion of a project.
    /// </summary>
    Delete,
}
