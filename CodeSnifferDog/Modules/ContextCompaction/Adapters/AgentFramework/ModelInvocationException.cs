using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class ModelInvocationException(
    ModelInvocationFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ModelInvocationFailureKind FailureKind { get; } = failureKind;
}
