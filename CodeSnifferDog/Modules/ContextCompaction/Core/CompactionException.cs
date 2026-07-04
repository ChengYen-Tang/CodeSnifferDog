namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Represents a failure in the compaction pipeline or its summary contract.
/// </summary>
public sealed class CompactionException : Exception
{
    /// <summary>
    /// Initializes a new exception with a compaction-specific error message.
    /// </summary>
    /// <param name="message">Message that describes the compaction failure.</param>
    public CompactionException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new exception with a compaction-specific error message and underlying cause.
    /// </summary>
    /// <param name="message">Message that describes the compaction failure.</param>
    /// <param name="innerException">Underlying exception that caused the compaction failure.</param>
    public CompactionException(string message, Exception innerException)
        : base(message, innerException) { }
}
