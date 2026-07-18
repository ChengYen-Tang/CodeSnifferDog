using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Holds sidebar snapshot, UI, and transport state as one cohesive client-side model.
/// </summary>
/// <param name="snapshot">Snapshot state for the sidebar data tree.</param>
/// <param name="ui">UI state such as selection and group expansion.</param>
/// <param name="transport">Transport state such as loading and live connection status.</param>
public sealed class State(
    SnapshotState snapshot,
    UiState ui,
    TransportState transport)
{
    /// <summary>
    /// Gets the current snapshot state.
    /// </summary>
    public SnapshotState Snapshot { get; } = snapshot;

    /// <summary>
    /// Gets the current UI state.
    /// </summary>
    public UiState Ui { get; } = ui;

    /// <summary>
    /// Gets the current transport state.
    /// </summary>
    public TransportState Transport { get; } = transport;

    /// <summary>
    /// Creates the empty sidebar state used before the first snapshot arrives.
    /// </summary>
    /// <returns>An empty sidebar state.</returns>
    public static State CreateEmpty() =>
        new(
            new SnapshotState(null),
            new UiState(selectedProjectId: null, groupExpansionStates: []),
            new TransportState(
                isLoading: true,
                isReconnecting: false,
                isLiveConnected: false,
                snapshotErrorMessage: null,
                liveErrorMessage: null,
                isPollingFallbackActive: true));

    /// <summary>
    /// Applies a replacement snapshot and reconciles the URI-derived selection against its available projects.
    /// </summary>
    /// <param name="snapshot">Replacement sidebar snapshot.</param>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    /// <returns><see langword="true" /> when either snapshot state or UI state changed.</returns>
    public bool ApplySnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        bool snapshotChanged = Snapshot.Update(snapshot);
        bool uiChanged = Ui.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        return snapshotChanged || uiChanged;
    }
}

/// <summary>
/// Holds the latest sidebar snapshot and reports whether visible state changed when it is replaced.
/// </summary>
/// <param name="snapshot">Initial sidebar snapshot.</param>
public sealed class SnapshotState(ProjectSidebarSnapshotDto? snapshot)
{
    /// <summary>
    /// Gets the current sidebar snapshot, if one exists.
    /// </summary>
    public ProjectSidebarSnapshotDto? Snapshot { get; private set; } = snapshot;

    /// <summary>
    /// Gets the current sidebar groups, or an empty list when no snapshot exists.
    /// </summary>
    public IReadOnlyList<ProjectSidebarGroupDto> Groups => Snapshot?.Groups ?? [];

    /// <summary>
    /// Replaces the snapshot and reports whether the visible sidebar state changed.
    /// </summary>
    /// <param name="snapshot">Replacement sidebar snapshot.</param>
    /// <returns><see langword="true" /> when the visible state changed.</returns>
    public bool Update(ProjectSidebarSnapshotDto? snapshot)
    {
        bool changed = !SnapshotComparer.HasEquivalentVisibleState(Snapshot, snapshot);
        Snapshot = snapshot;
        return changed;
    }
}

/// <summary>
/// Holds sidebar UI state such as selected project and group expansion.
/// </summary>
/// <param name="selectedProjectId">Initially selected project identifier.</param>
/// <param name="groupExpansionStates">Initial group expansion states keyed by group key.</param>
public sealed class UiState(string? selectedProjectId, Dictionary<string, bool> groupExpansionStates)
{
    /// <summary>
    /// Gets the currently selected project identifier, if any.
    /// </summary>
    public string? SelectedProjectId { get; private set; } = selectedProjectId;

    /// <summary>
    /// Gets the current group expansion states keyed by group key.
    /// </summary>
    public Dictionary<string, bool> GroupExpansionStates { get; } = groupExpansionStates;

    /// <summary>
    /// Reconciles selection and group expansion against a replacement snapshot and optional URI selection.
    /// </summary>
    /// <param name="snapshot">Replacement sidebar snapshot.</param>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    /// <returns><see langword="true" /> when selection or expansion state changed.</returns>
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

        SelectedProjectId = null;
        return HasChanged(previousSelectedProjectId, previousExpansionStates);
    }

    /// <summary>
    /// Reconciles the selected project against a project identifier from the current URI.
    /// </summary>
    /// <param name="selectedProjectIdFromUri">Project identifier from the URI.</param>
    /// <param name="groups">Current sidebar groups used to validate the identifier.</param>
    /// <returns><see langword="true" /> when the selection changed.</returns>
    public bool SyncSelectedProjectFromUri(string? selectedProjectIdFromUri, IReadOnlyList<ProjectSidebarGroupDto> groups)
    {
        bool projectExists = selectedProjectIdFromUri is not null && groups
            .SelectMany(group => group.Projects)
            .Any(project => string.Equals(project.ProjectId.ToString(), selectedProjectIdFromUri, StringComparison.Ordinal));

        string? nextSelectedProjectId = projectExists ? selectedProjectIdFromUri : null;
        if (string.Equals(SelectedProjectId, nextSelectedProjectId, StringComparison.Ordinal))
            return false;

        SelectedProjectId = nextSelectedProjectId;
        return true;
    }

    /// <summary>
    /// Gets whether one group is expanded.
    /// </summary>
    /// <param name="groupKey">Group key to inspect.</param>
    /// <param name="status">Project status used to derive the default state.</param>
    /// <returns><see langword="true" /> when the group is expanded.</returns>
    public bool IsExpanded(string groupKey, ProjectStatus status) =>
        GroupExpansionStates.TryGetValue(groupKey, out bool isExpanded)
            ? isExpanded
            : GetDefaultExpanded(status);

    /// <summary>
    /// Toggles one group's expanded state.
    /// </summary>
    /// <param name="groupKey">Group key to toggle.</param>
    /// <param name="status">Project status used to derive the default state.</param>
    /// <returns>Always <see langword="true" /> because toggling always mutates state.</returns>
    public bool ToggleGroup(string groupKey, ProjectStatus status)
    {
        bool currentValue = IsExpanded(groupKey, status);
        GroupExpansionStates[groupKey] = !currentValue;
        return true;
    }

    /// <summary>
    /// Determines whether selection or expansion state changed compared with a previous snapshot.
    /// </summary>
    /// <param name="previousSelectedProjectId">Previously selected project identifier.</param>
    /// <param name="previousExpansionStates">Previously stored group expansion states.</param>
    /// <returns><see langword="true" /> when selection or expansion state changed.</returns>
    private bool HasChanged(string? previousSelectedProjectId, IReadOnlyDictionary<string, bool> previousExpansionStates) =>
        !string.Equals(previousSelectedProjectId, SelectedProjectId, StringComparison.Ordinal) ||
        !HasEquivalentExpansionStates(previousExpansionStates);

    /// <summary>
    /// Compares current group expansion state against a previous snapshot.
    /// </summary>
    /// <param name="previousExpansionStates">Previously stored group expansion states.</param>
    /// <returns><see langword="true" /> when the expansion states are equivalent.</returns>
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

    /// <summary>
    /// Gets the default expanded state for a group based on project status.
    /// </summary>
    /// <param name="status">Project status associated with the group.</param>
    /// <returns><see langword="true" /> when the group should be expanded by default.</returns>
    private static bool GetDefaultExpanded(ProjectStatus status) => status switch
    {
        ProjectStatus.Reviewing or ProjectStatus.Completed or ProjectStatus.Queued => true,
        _ => false,
    };
}

/// <summary>
/// Holds transport state such as loading, reconnecting, live connection, and fallback polling flags.
/// </summary>
/// <param name="isLoading">Initial snapshot-loading flag.</param>
/// <param name="isReconnecting">Initial reconnecting flag.</param>
/// <param name="isLiveConnected">Initial live-connection flag.</param>
/// <param name="snapshotErrorMessage">Initial snapshot error message.</param>
/// <param name="liveErrorMessage">Initial live connection error message.</param>
/// <param name="isPollingFallbackActive">Initial polling-fallback flag.</param>
public sealed class TransportState(
    bool isLoading,
    bool isReconnecting,
    bool isLiveConnected,
    string? snapshotErrorMessage,
    string? liveErrorMessage,
    bool isPollingFallbackActive)
{
    /// <summary>
    /// Gets whether the sidebar is performing an initial snapshot load.
    /// </summary>
    public bool IsLoading { get; private set; } = isLoading;

    /// <summary>
    /// Gets whether the live transport is currently reconnecting.
    /// </summary>
    public bool IsReconnecting { get; private set; } = isReconnecting;

    /// <summary>
    /// Gets whether push-based live updates are currently connected.
    /// </summary>
    public bool IsLiveConnected { get; private set; } = isLiveConnected;

    /// <summary>
    /// Gets the latest snapshot-load error message, when one exists.
    /// </summary>
    public string? SnapshotErrorMessage { get; private set; } = snapshotErrorMessage;

    /// <summary>
    /// Gets the latest live connection error message, when one exists.
    /// </summary>
    public string? LiveErrorMessage { get; private set; } = liveErrorMessage;

    /// <summary>
    /// Gets whether polling fallback is currently active.
    /// </summary>
    public bool IsPollingFallbackActive { get; private set; } = isPollingFallbackActive;

    /// <summary>
    /// Switches the transport state into initial-load mode.
    /// </summary>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    public bool StartInitialLoad()
    {
        bool changed = !IsLoading || SnapshotErrorMessage is not null;
        IsLoading = true;
        SnapshotErrorMessage = null;
        return changed;
    }

    /// <summary>
    /// Marks a non-initial refresh as started.
    /// </summary>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    public bool StartRefresh()
    {
        bool changed = IsLoading || SnapshotErrorMessage is not null;
        IsLoading = false;
        SnapshotErrorMessage = null;
        return changed;
    }

    /// <summary>
    /// Completes snapshot loading and stores the latest snapshot error message.
    /// </summary>
    /// <param name="snapshotErrorMessage">Snapshot error message, or <see langword="null" /> when the load succeeded.</param>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    public bool CompleteSnapshotLoad(string? snapshotErrorMessage = null)
    {
        bool changed = IsLoading || !string.Equals(SnapshotErrorMessage, snapshotErrorMessage, StringComparison.Ordinal);
        IsLoading = false;
        SnapshotErrorMessage = snapshotErrorMessage;
        return changed;
    }

    /// <summary>
    /// Updates the live connection flag and latest live error message.
    /// </summary>
    /// <param name="isLiveConnected">Whether push-based live updates are connected.</param>
    /// <param name="liveErrorMessage">Latest live connection error message, or <see langword="null" /> when none exists.</param>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    public bool SetLiveConnected(bool isLiveConnected, string? liveErrorMessage = null)
    {
        bool changed = IsLiveConnected != isLiveConnected ||
            !string.Equals(LiveErrorMessage, liveErrorMessage, StringComparison.Ordinal);
        IsLiveConnected = isLiveConnected;
        LiveErrorMessage = liveErrorMessage;
        return changed;
    }

    /// <summary>
    /// Updates the reconnecting flag.
    /// </summary>
    /// <param name="isReconnecting">Replacement reconnecting flag.</param>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    public bool SetReconnecting(bool isReconnecting)
    {
        bool changed = IsReconnecting != isReconnecting;
        IsReconnecting = isReconnecting;
        return changed;
    }

    /// <summary>
    /// Updates the polling-fallback-active flag.
    /// </summary>
    /// <param name="isPollingFallbackActive">Replacement polling-fallback flag.</param>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    public bool SetPollingFallbackActive(bool isPollingFallbackActive)
    {
        bool changed = IsPollingFallbackActive != isPollingFallbackActive;
        IsPollingFallbackActive = isPollingFallbackActive;
        return changed;
    }
}
