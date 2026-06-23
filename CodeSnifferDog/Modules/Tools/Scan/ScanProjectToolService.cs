using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;

namespace CodeSnifferDog.Modules.Tools.Scan;

internal sealed class ScanProjectToolService(IScanProjectStore scanProjectStore)
{
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;

    public async ValueTask<AddScanProjectResult> AddScanProjectAsync(
        AddScanProjectArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ValidateAddScanProjectArgs(args);

        StoredScanProject storedProject = await _scanProjectStore.AddAsync(
            CreateProject(args),
            cancellationToken).ConfigureAwait(false);

        return new AddScanProjectResult
        {
            ScanProjectId = storedProject.ScanProjectId,
        };
    }

    public async ValueTask<AddScanProjectsResult> AddScanProjectsAsync(
        AddScanProjectsArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Projects.Count == 0)
            throw new ArgumentException("At least one scan project is required.", nameof(args));

        foreach (AddScanProjectArgs project in args.Projects)
            ValidateAddScanProjectArgs(project);

        IReadOnlyList<StoredScanProject> storedProjects = await _scanProjectStore.AddRangeAsync(
            [.. args.Projects.Select(CreateProject)],
            cancellationToken).ConfigureAwait(false);

        return new AddScanProjectsResult
        {
            ScanProjectIds = [.. storedProjects.Select(project => project.ScanProjectId)],
        };
    }

    public ValueTask<bool> DeleteScanProjectAsync(DeleteScanProjectArgs args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ScanProjectId);
        return _scanProjectStore.DeleteAsync(args.ScanProjectId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsAsync(CancellationToken cancellationToken)
        =>
        _scanProjectStore.ListAsync(cancellationToken);

    private static ScanProject CreateProject(AddScanProjectArgs args) =>
        new()
        {
            ProjectName = args.ProjectName.Trim(),
            ProjectPath = args.ProjectPath.Trim(),
            ProjectType = args.ProjectType.Trim(),
            Reason = args.Reason.Trim(),
        };

    private static void ValidateAddScanProjectArgs(AddScanProjectArgs args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Reason);
    }
}
