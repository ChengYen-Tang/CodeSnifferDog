namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Selects which exception type should surface when a persisted project status is unsupported.
/// </summary>
internal enum ProjectStatusMappingExceptionStyle
{
    /// <summary>
    /// Uses an argument-range exception suitable for surface-level callers.
    /// </summary>
    Surface,

    /// <summary>
    /// Uses an invalid-operation exception suitable for persisted-state failures.
    /// </summary>
    Persisted,
}
