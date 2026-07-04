using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>
/// Retries only failures that indicate the request exceeded model context or media limits.
/// </summary>
public sealed class DefaultReactiveExceptionDecider : IReactiveExceptionDecider
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <see langword="null" />.</exception>
    public bool ShouldRetryWithReactiveCompaction(ModelInvocationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.FailureKind is
            ModelInvocationFailureKind.ContextWindowExceeded or
            ModelInvocationFailureKind.MediaPayloadTooLarge;
    }
}
