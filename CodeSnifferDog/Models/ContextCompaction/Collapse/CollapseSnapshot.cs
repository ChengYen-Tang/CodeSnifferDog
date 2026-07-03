
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

public sealed class CollapseSnapshot
{
    public IReadOnlyList<string> ProjectedCollapseIds { get; init; } = [];

    public string? LastCommittedCollapseId { get; init; }

    public string? LastStagedCollapseId { get; init; }

    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    public bool Armed { get; init; }

    public int? LastSpawnTokens { get; init; }

    public DateTimeOffset? LastArmedAtUtc { get; init; }
}
