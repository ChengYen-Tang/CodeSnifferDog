using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class DefaultReactiveExceptionDecider : IReactiveExceptionDecider
{
    public bool ShouldRetryWithReactiveCompaction(ModelInvocationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.FailureKind is
            ModelInvocationFailureKind.ContextWindowExceeded or
            ModelInvocationFailureKind.MediaPayloadTooLarge;
    }
}
