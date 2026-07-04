
namespace CodeSnifferDog.Models.ContextCompaction.Failures;

/// <summary>
/// Classifies model invocation failures relevant to reactive compaction decisions.
/// </summary>
public enum ModelInvocationFailureKind
{
    /// <summary>
    /// Failure kind could not be classified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The model invocation exceeded the available context window.
    /// </summary>
    ContextWindowExceeded = 1,

    /// <summary>
    /// The model invocation failed because media payloads were too large.
    /// </summary>
    MediaPayloadTooLarge = 2,
}
