
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

/// <summary>
/// Captures the projection and arming state used by context-collapse orchestration.
/// </summary>
public sealed class CollapseSnapshot
{
    /// <summary>
    /// Gets the projected collapse identifiers currently considered for the session.
    /// </summary>
    public IReadOnlyList<string> ProjectedCollapseIds { get; init; } = [];

    /// <summary>
    /// Gets the identifier of the last committed collapse span, when one exists.
    /// </summary>
    public string? LastCommittedCollapseId { get; init; }

    /// <summary>
    /// Gets the identifier of the last staged collapse span, when one exists.
    /// </summary>
    public string? LastStagedCollapseId { get; init; }

    /// <summary>
    /// Gets when collapse projection was last recomputed, in UTC.
    /// </summary>
    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    /// <summary>
    /// Gets whether the collapse controller is currently armed.
    /// </summary>
    public bool Armed { get; init; }

    /// <summary>
    /// Gets the token count observed when the controller was last armed, when one exists.
    /// </summary>
    public int? LastSpawnTokens { get; init; }

    /// <summary>
    /// Gets when the controller was last armed, in UTC.
    /// </summary>
    public DateTimeOffset? LastArmedAtUtc { get; init; }
}
