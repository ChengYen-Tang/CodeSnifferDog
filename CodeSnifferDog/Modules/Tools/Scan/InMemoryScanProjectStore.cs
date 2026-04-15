using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Modules.Tools.Scan;

public sealed class InMemoryScanProjectStore : IScanProjectStore
{
    private readonly List<StoredScanProject> _projects = [];
    private readonly Lock _syncRoot = new();

    public ValueTask<StoredScanProject> AddAsync(ScanProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidateScanProject(project);

        StoredScanProject storedProject = new()
        {
            ScanProjectId = Guid.NewGuid().ToString("N"),
            ProjectName = project.ProjectName.Trim(),
            ProjectPath = project.ProjectPath.Trim(),
            ProjectType = project.ProjectType.Trim(),
            Reason = project.Reason.Trim(),
        };

        lock (_syncRoot)
            _projects.Add(storedProject);

        return ValueTask.FromResult(storedProject);
    }

    public async ValueTask<IReadOnlyList<StoredScanProject>> AddRangeAsync(
        IReadOnlyList<ScanProject> projects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projects);

        if (projects.Count == 0)
            throw new ArgumentException("At least one scan project is required.", nameof(projects));

        List<StoredScanProject> storedProjects = [];

        foreach (ScanProject project in projects)
            storedProjects.Add(await AddAsync(project, cancellationToken).ConfigureAwait(false));

        return storedProjects;
    }

    public ValueTask<bool> DeleteAsync(string scanProjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanProjectId);

        lock (_syncRoot)
        {
            StoredScanProject? existingProject = _projects.FirstOrDefault(project => project.ScanProjectId == scanProjectId);

            if (existingProject is null)
                return ValueTask.FromResult(false);

            _projects.Remove(existingProject);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<IReadOnlyList<StoredScanProject>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredScanProject>>([.. _projects]);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            _projects.Clear();

        return ValueTask.CompletedTask;
    }

    private static void ValidateScanProject(ScanProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Reason);
    }
}
