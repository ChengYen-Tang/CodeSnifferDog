namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

/// <summary>
/// Selects which error wording should surface when projecting unsupported persisted values.
/// </summary>
internal enum ExceptionStyle
{
    /// <summary>
    /// Wording for persisted-state failures.
    /// </summary>
    Persisted,

    /// <summary>
    /// Wording for snapshot/backfill projection failures.
    /// </summary>
    Snapshot,
}
