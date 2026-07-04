using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;

namespace CodeSnifferDog.Modules.Tools.Scan;

/// <summary>
/// Validates scan tool arguments and delegates storage operations to <see cref="IScanProjectStore" />.
/// </summary>
internal sealed class ScanProjectToolService(IScanProjectStore scanProjectStore)
{
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;

    /// <summary>
    /// Adds one discovered scan project.
    /// </summary>
    /// <param name="args">Arguments that describe the discovered project.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The generated stored project identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a required argument field is missing.</exception>
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

    /// <summary>
    /// Adds multiple discovered scan projects.
    /// </summary>
    /// <param name="args">Arguments that describe the discovered projects.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The generated stored project identifiers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no projects are supplied or a required field is missing.</exception>
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

    /// <summary>
    /// Deletes one stored scan project.
    /// </summary>
    /// <param name="args">Delete arguments.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the project was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="DeleteScanProjectArgs.ScanProjectId"/> is missing.</exception>
    public ValueTask<bool> DeleteScanProjectAsync(DeleteScanProjectArgs args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ScanProjectId);
        return _scanProjectStore.DeleteAsync(args.ScanProjectId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Lists all stored scan projects.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The stored scan projects.</returns>
    public ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsAsync(CancellationToken cancellationToken)
        =>
        _scanProjectStore.ListAsync(cancellationToken);

    /// <summary>
    /// Creates a normalized scan project from tool arguments.
    /// </summary>
    /// <param name="args">Arguments to normalize.</param>
    /// <returns>The normalized scan project.</returns>
    private static ScanProject CreateProject(AddScanProjectArgs args) =>
        new()
        {
            ProjectName = args.ProjectName.Trim(),
            ProjectPath = args.ProjectPath.Trim(),
            ProjectType = args.ProjectType.Trim(),
            Reason = args.Reason.Trim(),
        };

    /// <summary>
    /// Validates one scan project argument payload.
    /// </summary>
    /// <param name="args">Arguments to validate.</param>
    private static void ValidateAddScanProjectArgs(AddScanProjectArgs args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Reason);
    }
}
