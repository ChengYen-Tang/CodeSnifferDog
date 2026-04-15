using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class OperationalContextModelInvocationException : Exception
{
    public OperationalContextModelInvocationException(
        OperationalContextModelInvocationFailureKind failureKind,
        string message,
        Exception? innerException = null)
        : base(message, innerException) => FailureKind = failureKind;

    public OperationalContextModelInvocationFailureKind FailureKind { get; }
}
