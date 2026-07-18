using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Coordinates sidebar snapshot state, live refresh, selection, and project actions for the projects sidebar UI.
/// </summary>
public interface IController
{
    /// <summary>
    /// Gets the current sidebar state.
    /// </summary>
    State Current { get; }

    /// <summary>
    /// Raised when <see cref="Current" /> changes.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Seeds the sidebar with an initial snapshot and optional URI-selected project.
    /// </summary>
    /// <param name="snapshot">Initial sidebar snapshot, when one is already available.</param>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri);

    /// <summary>
    /// Toggles the expansion state of one project group.
    /// </summary>
    /// <param name="groupKey">Group key to toggle.</param>
    /// <param name="status">Project status used to derive the default expansion state.</param>
    void ToggleGroup(string groupKey, ProjectStatus status);

    /// <summary>
    /// Reconciles the selected project against the current URI. The URI is the sole source of selection.
    /// </summary>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri);

    /// <summary>
    /// Deletes one project and refreshes sidebar state afterwards.
    /// </summary>
    /// <param name="projectId">Project identifier to delete.</param>
    /// <param name="cancellationToken">Cancels the delete and any follow-up refresh.</param>
    /// <returns><see langword="true" /> when the project existed and was deleted; otherwise <see langword="false" />.</returns>
    Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels one project execution and refreshes sidebar state afterwards.
    /// </summary>
    /// <param name="projectId">Project identifier to cancel.</param>
    /// <param name="cancellationToken">Cancels the request and any follow-up refresh.</param>
    /// <returns><see langword="true" /> when the project existed and accepted cancellation; otherwise <see langword="false" />.</returns>
    Task<bool> CancelProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the sidebar controller, loading an initial snapshot when needed and attaching live refresh behavior.
    /// </summary>
    /// <param name="initialSnapshot">Optional initial sidebar snapshot.</param>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    /// <param name="cancellationToken">Cancels startup and live-refresh initialization.</param>
    Task StartAsync(
        ProjectSidebarSnapshotDto? initialSnapshot = null,
        string? selectedProjectIdFromUri = null,
        CancellationToken cancellationToken = default);
}
