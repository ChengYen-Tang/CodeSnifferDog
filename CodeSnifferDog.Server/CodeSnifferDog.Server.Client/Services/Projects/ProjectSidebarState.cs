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
                isReconnecting: false,
                isLiveConnected: false,
                snapshotErrorMessage: null,
                liveErrorMessage: null,
                isPollingFallbackActive: true));

    public bool ApplySnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        bool snapshotChanged = Snapshot.Update(snapshot);
        bool uiChanged = Ui.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        return snapshotChanged || uiChanged;
    }
}

public sealed class ProjectSidebarSnapshotState(ProjectSidebarSnapshotDto? snapshot)
{
    public ProjectSidebarSnapshotDto? Snapshot { get; private set; } = snapshot;

    public IReadOnlyList<ProjectSidebarGroupDto> Groups => Snapshot?.Groups ?? [];

    public bool Update(ProjectSidebarSnapshotDto? snapshot)
    {
        bool changed = !ProjectSidebarSnapshotComparer.HasEquivalentVisibleState(Snapshot, snapshot);
        Snapshot = snapshot;
        return changed;
    }
}

public sealed class ProjectSidebarUiState(string? selectedProjectId, Dictionary<string, bool> groupExpansionStates)
{
    public string? SelectedProjectId { get; private set; } = selectedProjectId;

    public Dictionary<string, bool> GroupExpansionStates { get; } = groupExpansionStates;

    public bool ApplySnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        string? previousSelectedProjectId = SelectedProjectId;
        Dictionary<string, bool> previousExpansionStates = new(GroupExpansionStates, StringComparer.Ordinal);
        IReadOnlyList<ProjectSidebarGroupDto> groups = snapshot?.Groups ?? [];
        HashSet<string> validProjectIds = groups
            .SelectMany(group => group.Projects)
            .Select(project => project.ProjectId.ToString())
            .ToHashSet(StringComparer.Ordinal);

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
            return HasChanged(previousSelectedProjectId, previousExpansionStates);
        }

        if (SelectedProjectId is not null && validProjectIds.Contains(SelectedProjectId))
            return HasChanged(previousSelectedProjectId, previousExpansionStates);

        if (snapshot?.SelectedProjectId is Guid selectedProjectIdFromSnapshot)
        {
            string selectedProjectIdText = selectedProjectIdFromSnapshot.ToString();
            if (validProjectIds.Contains(selectedProjectIdText))
            {
                SelectedProjectId = selectedProjectIdText;
                return HasChanged(previousSelectedProjectId, previousExpansionStates);
            }
        }

        SelectedProjectId = groups
            .SelectMany(group => group.Projects.OrderBy(project => project.SortOrder))
            .Select(project => project.ProjectId.ToString())
            .FirstOrDefault();
        return HasChanged(previousSelectedProjectId, previousExpansionStates);
    }

    public bool SelectProject(string projectId)
    {
        if (string.Equals(SelectedProjectId, projectId, StringComparison.Ordinal))
            return false;

        SelectedProjectId = projectId;
        return true;
    }

    public bool SyncSelectedProjectFromUri(string? selectedProjectIdFromUri, IReadOnlyList<ProjectSidebarGroupDto> groups)
    {
        if (selectedProjectIdFromUri is null)
            return false;

        bool projectExists = groups
            .SelectMany(group => group.Projects)
            .Any(project => string.Equals(project.ProjectId.ToString(), selectedProjectIdFromUri, StringComparison.Ordinal));

        if (!projectExists || string.Equals(SelectedProjectId, selectedProjectIdFromUri, StringComparison.Ordinal))
            return false;

        SelectedProjectId = selectedProjectIdFromUri;
        return true;
    }

    public bool IsExpanded(string groupKey, ProjectStatus status) =>
        GroupExpansionStates.TryGetValue(groupKey, out bool isExpanded)
            ? isExpanded
            : GetDefaultExpanded(status);

    public bool ToggleGroup(string groupKey, ProjectStatus status)
    {
        bool currentValue = IsExpanded(groupKey, status);
        GroupExpansionStates[groupKey] = !currentValue;
        return true;
    }

    private bool HasChanged(string? previousSelectedProjectId, IReadOnlyDictionary<string, bool> previousExpansionStates) =>
        !string.Equals(previousSelectedProjectId, SelectedProjectId, StringComparison.Ordinal) ||
        !HasEquivalentExpansionStates(previousExpansionStates);

    private bool HasEquivalentExpansionStates(IReadOnlyDictionary<string, bool> previousExpansionStates)
    {
        if (previousExpansionStates.Count != GroupExpansionStates.Count)
            return false;

        foreach (KeyValuePair<string, bool> previousState in previousExpansionStates)
        {
            if (!GroupExpansionStates.TryGetValue(previousState.Key, out bool currentValue) ||
                currentValue != previousState.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool GetDefaultExpanded(ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing or ProjectStatus.Completed or ProjectStatus.Queued => true,
        _ => false,
    };
}

public sealed class ProjectSidebarTransportState(
    bool isLoading,
    bool isReconnecting,
    bool isLiveConnected,
    string? snapshotErrorMessage,
    string? liveErrorMessage,
    bool isPollingFallbackActive)
{
    public bool IsLoading { get; private set; } = isLoading;

    public bool IsReconnecting { get; private set; } = isReconnecting;

    public bool IsLiveConnected { get; private set; } = isLiveConnected;

    public string? SnapshotErrorMessage { get; private set; } = snapshotErrorMessage;

    public string? LiveErrorMessage { get; private set; } = liveErrorMessage;

    public bool IsPollingFallbackActive { get; private set; } = isPollingFallbackActive;

    public bool StartInitialLoad()
    {
        bool changed = !IsLoading || SnapshotErrorMessage is not null;
        IsLoading = true;
        SnapshotErrorMessage = null;
        return changed;
    }

    public bool StartRefresh()
    {
        bool changed = IsLoading || SnapshotErrorMessage is not null;
        IsLoading = false;
        SnapshotErrorMessage = null;
        return changed;
    }

    public bool CompleteSnapshotLoad(string? snapshotErrorMessage = null)
    {
        bool changed = IsLoading || !string.Equals(SnapshotErrorMessage, snapshotErrorMessage, StringComparison.Ordinal);
        IsLoading = false;
        SnapshotErrorMessage = snapshotErrorMessage;
        return changed;
    }

    public bool SetLiveConnected(bool isLiveConnected, string? liveErrorMessage = null)
    {
        bool changed = IsLiveConnected != isLiveConnected ||
            !string.Equals(LiveErrorMessage, liveErrorMessage, StringComparison.Ordinal);
        IsLiveConnected = isLiveConnected;
        LiveErrorMessage = liveErrorMessage;
        return changed;
    }

    public bool SetReconnecting(bool isReconnecting)
    {
        bool changed = IsReconnecting != isReconnecting;
        IsReconnecting = isReconnecting;
        return changed;
    }

    public bool SetPollingFallbackActive(bool isPollingFallbackActive)
    {
        bool changed = IsPollingFallbackActive != isPollingFallbackActive;
        IsPollingFallbackActive = isPollingFallbackActive;
        return changed;
    }
}
