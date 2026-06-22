using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Modules.Tools.Scan.State;

internal sealed class ScanProjectStateStore
{
    private readonly List<StoredScanProject> _projects = [];

    public StoredScanProject Add(StoredScanProject storedProject)
    {
        StoredScanProject? existingProject = _projects.FirstOrDefault(candidate =>
            string.Equals(candidate.ProjectName, storedProject.ProjectName, StringComparison.Ordinal) &&
            string.Equals(candidate.ProjectPath, storedProject.ProjectPath, StringComparison.Ordinal) &&
            string.Equals(candidate.ProjectType, storedProject.ProjectType, StringComparison.Ordinal) &&
            string.Equals(candidate.Reason, storedProject.Reason, StringComparison.Ordinal));

        if (existingProject is not null)
            return existingProject;

        _projects.Add(storedProject);
        return storedProject;
    }

    public bool Delete(string scanProjectId)
    {
        StoredScanProject? existingProject = _projects.FirstOrDefault(project => project.ScanProjectId == scanProjectId);

        if (existingProject is null)
            return false;

        _projects.Remove(existingProject);
        return true;
    }

    public IReadOnlyList<StoredScanProject> List() =>
        [.. _projects];

    public void Clear() =>
        _projects.Clear();

    public IReadOnlyList<StoredScanProject> Clone() =>
        [.. _projects.Select(CloneProject)];

    public void Restore(IReadOnlyList<StoredScanProject> snapshot)
    {
        _projects.Clear();
        _projects.AddRange(snapshot.Select(CloneProject));
    }

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

    private static void Validate(ScanProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Reason);
    }

    private static StoredScanProject CloneProject(StoredScanProject project) =>
        new()
        {
            ScanProjectId = project.ScanProjectId,
            ProjectName = project.ProjectName,
            ProjectPath = project.ProjectPath,
            ProjectType = project.ProjectType,
            Reason = project.Reason,
        };
}
