using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Modules.Tools.Scan;

public interface IScanProjectStore : CodeSnifferDog.Workflows.Common.IRetrySafeAgentStore
{
    ValueTask<StoredScanProject> AddAsync(ScanProject project, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredScanProject>> AddRangeAsync(
        IReadOnlyList<ScanProject> projects,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(string scanProjectId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredScanProject>> ListAsync(CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);
}
