namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class CompactionException : Exception
{
    public CompactionException(string message)
        : base(message) { }

    public CompactionException(string message, Exception innerException)
        : base(message, innerException) { }
}
