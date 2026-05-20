using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Scan;

public sealed class InMemoryScanProjectStore : IScanProjectStore
{
    private readonly List<StoredScanProject> _projects = [];
    private readonly Lock _syncRoot = new();
    private Guid? _activeAttemptId;

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
        {
            if (!CanWrite())
                return ValueTask.FromResult(storedProject);

            StoredScanProject? existingProject = _projects.FirstOrDefault(candidate =>
                string.Equals(candidate.ProjectName, storedProject.ProjectName, StringComparison.Ordinal) &&
                string.Equals(candidate.ProjectPath, storedProject.ProjectPath, StringComparison.Ordinal) &&
                string.Equals(candidate.ProjectType, storedProject.ProjectType, StringComparison.Ordinal) &&
                string.Equals(candidate.Reason, storedProject.Reason, StringComparison.Ordinal));

            if (existingProject is not null)
                return ValueTask.FromResult(existingProject);

            _projects.Add(storedProject);
        }

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
            if (!CanWrite())
                return ValueTask.FromResult(false);

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
        {
            if (!CanWrite())
                return ValueTask.CompletedTask;

            _projects.Clear();
        }

        return ValueTask.CompletedTask;
    }

    public IAgentAttemptLease BeginAttempt(Guid attemptId)
    {
        lock (_syncRoot)
        {
            Guid staleWriteBlockerAttemptId = Guid.NewGuid();
            List<StoredScanProject> snapshot =
            [
                .. _projects.Select(static project => new StoredScanProject
                {
                    ScanProjectId = project.ScanProjectId,
                    ProjectName = project.ProjectName,
                    ProjectPath = project.ProjectPath,
                    ProjectType = project.ProjectType,
                    Reason = project.Reason,
                })
            ];
            _activeAttemptId = attemptId;

            return new AgentAttemptLease(() =>
            {
                lock (_syncRoot)
                {
                    _activeAttemptId = staleWriteBlockerAttemptId;
                    _projects.Clear();
                    _projects.AddRange(snapshot.Select(static project => new StoredScanProject
                    {
                        ScanProjectId = project.ScanProjectId,
                        ProjectName = project.ProjectName,
                        ProjectPath = project.ProjectPath,
                        ProjectType = project.ProjectType,
                        Reason = project.Reason,
                    }));
                }
            });
        }
    }

    private bool CanWrite()
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null || _activeAttemptId is null || currentAttemptId == _activeAttemptId;
    }
    private static void ValidateScanProject(ScanProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Reason);
    }
}
