using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Modules.Tools.Scan;

/// <summary>
/// Stores scan projects produced during one scan workflow run.
/// </summary>
public interface IScanProjectStore : CodeSnifferDog.Workflows.Common.IRetrySafeAgentStore
{
    /// <summary>
    /// Adds one discovered scan project.
    /// </summary>
    /// <param name="project">Scan project to add.</param>
    /// <param name="cancellationToken">Token that cancels the store operation.</param>
    /// <returns>The stored project.</returns>
    ValueTask<StoredScanProject> AddAsync(ScanProject project, CancellationToken cancellationToken);

    /// <summary>
    /// Adds multiple discovered scan projects.
    /// </summary>
    /// <param name="projects">Scan projects to add.</param>
    /// <param name="cancellationToken">Token that cancels the store operation.</param>
    /// <returns>The stored projects.</returns>
    ValueTask<IReadOnlyList<StoredScanProject>> AddRangeAsync(
        IReadOnlyList<ScanProject> projects,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one stored scan project.
    /// </summary>
    /// <param name="scanProjectId">Stored project identifier.</param>
    /// <param name="cancellationToken">Token that cancels the store operation.</param>
    /// <returns><see langword="true"/> when a project was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(string scanProjectId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all stored scan projects.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the store operation.</param>
    /// <returns>The stored scan projects.</returns>
    ValueTask<IReadOnlyList<StoredScanProject>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears all stored scan projects.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the store operation.</param>
    ValueTask ClearAsync(CancellationToken cancellationToken);
}
