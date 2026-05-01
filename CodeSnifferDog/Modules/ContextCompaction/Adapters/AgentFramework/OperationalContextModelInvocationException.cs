using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class OperationalContextModelInvocationException(
    OperationalContextModelInvocationFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public OperationalContextModelInvocationFailureKind FailureKind { get; } = failureKind;
}
