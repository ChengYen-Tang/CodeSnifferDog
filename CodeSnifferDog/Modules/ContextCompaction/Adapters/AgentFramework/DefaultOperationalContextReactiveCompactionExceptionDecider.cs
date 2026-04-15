using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class DefaultOperationalContextReactiveCompactionExceptionDecider : IOperationalContextReactiveCompactionExceptionDecider
{
    public bool ShouldRetryWithReactiveCompaction(OperationalContextModelInvocationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.FailureKind is
            OperationalContextModelInvocationFailureKind.ContextWindowExceeded or
            OperationalContextModelInvocationFailureKind.MediaPayloadTooLarge;
    }
}
