using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Modules.Tools.Scan.State;

/// <summary>
/// Stores scan projects for the current tool run and supports snapshot rollback.
/// </summary>
internal sealed class ScanProjectStateStore
{
    private readonly List<StoredScanProject> _projects = [];

    /// <summary>
    /// Adds a stored project unless an equivalent project already exists.
    /// </summary>
    /// <param name="storedProject">Stored project to add.</param>
    /// <returns>The existing equivalent project or the added project.</returns>
    public StoredScanProject Add(StoredScanProject storedProject)
    {
        StoredScanProject? existingProject = _projects.FirstOrDefault(candidate =>
            string.Equals(candidate.ProjectName, storedProject.ProjectName, StringComparison.Ordinal) &&
            string.Equals(candidate.ProjectPath, storedProject.ProjectPath, StringComparison.Ordinal) &&
            string.Equals(candidate.ProjectType, storedProject.ProjectType, StringComparison.Ordinal) &&
            string.Equals(candidate.Reason, storedProject.Reason, StringComparison.Ordinal));

        if (existingProject is not null)
            return existingProject;

        _projects.Insert(FindInsertionIndex(storedProject.ScanProjectId), storedProject);
        return storedProject;
    }

    /// <summary>
    /// Deletes a stored project by its identifier.
    /// </summary>
    /// <param name="scanProjectId">Stored scan project identifier.</param>
    /// <returns><see langword="true"/> when a project was removed; otherwise, <see langword="false"/>.</returns>
    public bool Delete(string scanProjectId)
    {
        int index = FindIndex(scanProjectId.Trim());

        if (index < 0)
            return false;

        _projects.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Lists all stored projects for internal workflow aggregation.
    /// </summary>
    /// <returns>The stored projects.</returns>
    public IReadOnlyList<StoredScanProject> ListAll() =>
        [.. _projects];

    /// <summary>
    /// Lists at most <paramref name="take"/> stored projects after <paramref name="cursor"/>.
    /// </summary>
    public IReadOnlyList<StoredScanProject> ListPage(string? cursor, int take)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        int startIndex = string.IsNullOrWhiteSpace(cursor)
            ? 0
            : FindFirstAfter(cursor.Trim());
        int count = Math.Min(take, _projects.Count - startIndex);

        return count == 0
            ? []
            : _projects.GetRange(startIndex, count);
    }

    /// <summary>
    /// Clears all stored projects.
    /// </summary>
    public void Clear() =>
        _projects.Clear();

    /// <summary>
    /// Creates a snapshot clone of all stored projects.
    /// </summary>
    /// <returns>The cloned project snapshot.</returns>
    public IReadOnlyList<StoredScanProject> Clone() =>
        [.. _projects.Select(CloneProject)];

    /// <summary>
    /// Restores the store from a cloned snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot to restore.</param>
    public void Restore(IReadOnlyList<StoredScanProject> snapshot)
    {
        _projects.Clear();
        _projects.AddRange(snapshot.Select(CloneProject));
        _projects.Sort(static (left, right) => string.CompareOrdinal(left.ScanProjectId, right.ScanProjectId));
    }

    /// <summary>
    /// Creates a stored project from a scan project and generated identifier.
    /// </summary>
    /// <param name="project">Scan project to normalize.</param>
    /// <param name="scanProjectId">Generated stored project identifier.</param>
    /// <returns>The normalized stored project.</returns>
    public static StoredScanProject CreateStoredProject(ScanProject project, string scanProjectId)
    {
        Validate(project);

        return new StoredScanProject
        {
            ScanProjectId = scanProjectId,
            ProjectName = project.ProjectName.Trim(),
            ProjectPath = project.ProjectPath.Trim(),
            ProjectType = project.ProjectType.Trim(),
            Reason = project.Reason.Trim(),
        };
    }

    /// <summary>
    /// Validates that a scan project contains all required fields.
    /// </summary>
    /// <param name="project">Scan project to validate.</param>
    private static void Validate(ScanProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Reason);
    }

    /// <summary>
    /// Clones one stored project.
    /// </summary>
    /// <param name="project">Stored project to clone.</param>
    /// <returns>The cloned project.</returns>
    private static StoredScanProject CloneProject(StoredScanProject project) =>
        new()
        {
            ScanProjectId = project.ScanProjectId,
            ProjectName = project.ProjectName,
            ProjectPath = project.ProjectPath,
            ProjectType = project.ProjectType,
            Reason = project.Reason,
        };

    /// <summary>
    /// Finds the index of the specified scan project identifier.
    /// </summary>
    private int FindIndex(string scanProjectId)
    {
        int index = FindInsertionIndex(scanProjectId);
        return index < _projects.Count && string.Equals(
            _projects[index].ScanProjectId,
            scanProjectId,
            StringComparison.Ordinal)
            ? index
            : -1;
    }

    /// <summary>
    /// Finds the first insertion position for a scan project identifier.
    /// </summary>
    private int FindInsertionIndex(string scanProjectId)
    {
        int low = 0;
        int high = _projects.Count;

        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(_projects[middle].ScanProjectId, scanProjectId) < 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    /// <summary>
    /// Finds the first project whose identifier sorts after the supplied cursor.
    /// </summary>
    private int FindFirstAfter(string cursor)
    {
        int low = 0;
        int high = _projects.Count;

        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(_projects[middle].ScanProjectId, cursor) <= 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }
}
