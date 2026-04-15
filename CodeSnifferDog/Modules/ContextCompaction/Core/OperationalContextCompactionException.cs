namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextCompactionException : Exception
{
    public OperationalContextCompactionException(string message)
        : base(message) { }

    public OperationalContextCompactionException(string message, Exception innerException)
        : base(message, innerException) { }
}
