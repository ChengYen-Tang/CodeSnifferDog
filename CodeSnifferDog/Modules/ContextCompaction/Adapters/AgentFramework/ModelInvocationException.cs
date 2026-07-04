using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>
/// Wraps a model invocation failure with a normalized failure classification used by retry policy.
/// </summary>
/// <param name="failureKind">Normalized category of the model invocation failure.</param>
/// <param name="message">Failure message.</param>
/// <param name="innerException">Optional underlying exception.</param>
public sealed class ModelInvocationException(
    ModelInvocationFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the normalized failure category associated with the model invocation.
    /// </summary>
    public ModelInvocationFailureKind FailureKind { get; } = failureKind;
}
