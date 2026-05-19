using CodeSnifferDog.Server.Client.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects;

public sealed class ServerPrerenderProjectSidebarController : IProjectSidebarController
{
    public ProjectSidebarState Current { get; } = ProjectSidebarState.CreateEmpty();

    public event Action? Changed;

    public void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        Current.Transport.CompleteSnapshotLoad();
        NotifyChanged();
    }

    public void SelectProject(string projectId)
    {
        Current.Ui.SelectProject(projectId);
        NotifyChanged();
    }

    public void ToggleGroup(string groupKey, ProjectStatus status)
    {
        Current.Ui.ToggleGroup(groupKey, status);
        NotifyChanged();
    }

    public void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri)
    {
        Current.Ui.SyncSelectedProjectFromUri(selectedProjectIdFromUri, Current.Snapshot.Groups);
        NotifyChanged();
    }

    public Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> CancelProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task StartAsync(
        ProjectSidebarSnapshotDto? initialSnapshot = null,
        string? selectedProjectIdFromUri = null,
        CancellationToken cancellationToken = default)
    {
        if (initialSnapshot is not null)
            InitializeSnapshot(initialSnapshot, selectedProjectIdFromUri);

        return Task.CompletedTask;
    }

    private void NotifyChanged() => Changed?.Invoke();
}
