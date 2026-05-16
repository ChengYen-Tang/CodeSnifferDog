using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects;

public sealed class ProjectSidebarState(
    ProjectSidebarSnapshotState snapshot,
    ProjectSidebarUiState ui,
    ProjectSidebarTransportState transport)
{
    public ProjectSidebarSnapshotState Snapshot { get; } = snapshot;

    public ProjectSidebarUiState Ui { get; } = ui;

    public ProjectSidebarTransportState Transport { get; } = transport;

    public static ProjectSidebarState CreateEmpty() =>
        new(
            new ProjectSidebarSnapshotState(null),
            new ProjectSidebarUiState(selectedProjectId: null, groupExpansionStates: []),
            new ProjectSidebarTransportState(
                isLoading: true,
                isRefreshing: false,
                isReconnecting: false,
                isLiveConnected: false,
                snapshotErrorMessage: null,
                liveErrorMessage: null,
                isPollingFallbackActive: true));

    public void ApplySnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        Snapshot.Update(snapshot);
        Ui.ApplySnapshot(snapshot, selectedProjectIdFromUri);
    }
}

public sealed class ProjectSidebarSnapshotState(ProjectSidebarSnapshotDto? snapshot)
{
    public ProjectSidebarSnapshotDto? Snapshot { get; private set; } = snapshot;

    public IReadOnlyList<ProjectSidebarGroupDto> Groups => Snapshot?.Groups ?? [];

    public void Update(ProjectSidebarSnapshotDto? snapshot)
    {
        Snapshot = snapshot;
    }
}

public sealed class ProjectSidebarUiState(string? selectedProjectId, Dictionary<string, bool> groupExpansionStates)
{
    public string? SelectedProjectId { get; private set; } = selectedProjectId;

    public Dictionary<string, bool> GroupExpansionStates { get; } = groupExpansionStates;

    public void ApplySnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        IReadOnlyList<ProjectSidebarGroupDto> groups = snapshot?.Groups ?? [];
        HashSet<string> validProjectIds = groups
            .SelectMany(group => group.Projects)
            .Select(project => project.ProjectId.ToString())
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, bool> previousExpansionStates = new(GroupExpansionStates, StringComparer.Ordinal);
        GroupExpansionStates.Clear();
        foreach (ProjectSidebarGroupDto group in groups)
        {
            GroupExpansionStates[group.GroupKey] = previousExpansionStates.TryGetValue(group.GroupKey, out bool isExpanded)
                ? isExpanded
                : GetDefaultExpanded(group.Status);
        }

        if (selectedProjectIdFromUri is not null && validProjectIds.Contains(selectedProjectIdFromUri))
        {
            SelectedProjectId = selectedProjectIdFromUri;
            return;
        }

        if (SelectedProjectId is not null && validProjectIds.Contains(SelectedProjectId))
            return;

        if (snapshot?.SelectedProjectId is Guid selectedProjectIdFromSnapshot)
        {
            string selectedProjectIdText = selectedProjectIdFromSnapshot.ToString();
            if (validProjectIds.Contains(selectedProjectIdText))
            {
                SelectedProjectId = selectedProjectIdText;
                return;
            }
        }

        SelectedProjectId = groups
            .SelectMany(group => group.Projects.OrderBy(project => project.SortOrder))
            .Select(project => project.ProjectId.ToString())
            .FirstOrDefault();
    }

    public void SelectProject(string projectId)
    {
        SelectedProjectId = projectId;
    }

    public void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri, IReadOnlyList<ProjectSidebarGroupDto> groups)
    {
        if (selectedProjectIdFromUri is null)
            return;

        bool projectExists = groups
            .SelectMany(group => group.Projects)
            .Any(project => string.Equals(project.ProjectId.ToString(), selectedProjectIdFromUri, StringComparison.Ordinal));

        if (projectExists)
            SelectedProjectId = selectedProjectIdFromUri;
    }

    public bool IsExpanded(string groupKey, ProjectStatus status) =>
        GroupExpansionStates.TryGetValue(groupKey, out bool isExpanded)
            ? isExpanded
            : GetDefaultExpanded(status);

    public void ToggleGroup(string groupKey, ProjectStatus status)
    {
        bool currentValue = IsExpanded(groupKey, status);
        GroupExpansionStates[groupKey] = !currentValue;
    }

    private static bool GetDefaultExpanded(ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing or ProjectStatus.Completed or ProjectStatus.Queued => true,
        _ => false,
    };
}

public sealed class ProjectSidebarTransportState(
    bool isLoading,
    bool isRefreshing,
    bool isReconnecting,
    bool isLiveConnected,
    string? snapshotErrorMessage,
    string? liveErrorMessage,
    bool isPollingFallbackActive)
{
    public bool IsLoading { get; private set; } = isLoading;

    public bool IsRefreshing { get; private set; } = isRefreshing;

    public bool IsReconnecting { get; private set; } = isReconnecting;

    public bool IsLiveConnected { get; private set; } = isLiveConnected;

    public string? SnapshotErrorMessage { get; private set; } = snapshotErrorMessage;

    public string? LiveErrorMessage { get; private set; } = liveErrorMessage;

    public bool IsPollingFallbackActive { get; private set; } = isPollingFallbackActive;

    public void StartInitialLoad()
    {
        IsLoading = true;
        IsRefreshing = false;
        SnapshotErrorMessage = null;
    }

    public void StartRefresh()
    {
        IsLoading = false;
        IsRefreshing = true;
        SnapshotErrorMessage = null;
    }

    public void CompleteSnapshotLoad(string? snapshotErrorMessage = null)
    {
        IsLoading = false;
        IsRefreshing = false;
        SnapshotErrorMessage = snapshotErrorMessage;
    }

    public void SetLiveConnected(bool isLiveConnected, string? liveErrorMessage = null)
    {
        IsLiveConnected = isLiveConnected;
        LiveErrorMessage = liveErrorMessage;
    }

    public void SetReconnecting(bool isReconnecting)
    {
        IsReconnecting = isReconnecting;
    }

    public void SetPollingFallbackActive(bool isPollingFallbackActive)
    {
        IsPollingFallbackActive = isPollingFallbackActive;
    }
}
