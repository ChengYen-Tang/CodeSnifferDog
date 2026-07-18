using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar;

/// <summary>
/// Minimal sidebar controller used during server prerendering where mutations and live refresh are unavailable.
/// </summary>
public sealed class ServerPrerenderController : IController
{
    /// <inheritdoc />
    public State Current { get; } = State.CreateEmpty();

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        Current.Transport.CompleteSnapshotLoad();
        NotifyChanged();
    }

    /// <inheritdoc />
    public void ToggleGroup(string groupKey, ProjectStatus status)
    {
        Current.Ui.ToggleGroup(groupKey, status);
        NotifyChanged();
    }

    /// <inheritdoc />
    public void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri)
    {
        Current.Ui.SyncSelectedProjectFromUri(selectedProjectIdFromUri, Current.Snapshot.Groups);
        NotifyChanged();
    }

    /// <inheritdoc />
    public Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> CancelProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    public Task StartAsync(
        ProjectSidebarSnapshotDto? initialSnapshot = null,
        string? selectedProjectIdFromUri = null,
        CancellationToken cancellationToken = default)
    {
        if (initialSnapshot is not null)
            InitializeSnapshot(initialSnapshot, selectedProjectIdFromUri);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Raises the state-changed notification.
    /// </summary>
    private void NotifyChanged() => Changed?.Invoke();
}
