namespace CodeSnifferDog.Modules.Tools.Output;

/// <summary>
/// Tracks a shared UTF-8 output budget across command output streams.
/// </summary>
internal sealed class CommandOutputCaptureBudget
{
    /// <summary>
    /// Synchronizes budget updates from concurrent stream callbacks.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Stores the remaining combined UTF-8 byte budget.
    /// </summary>
    private int _remainingBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandOutputCaptureBudget"/> class.
    /// </summary>
    /// <param name="maxBytes">Maximum combined UTF-8 bytes to retain.</param>
    public CommandOutputCaptureBudget(int maxBytes)
    {
        _remainingBytes = Math.Max(0, maxBytes);
    }

    /// <summary>
    /// Captures the portion of one chunk that still fits within the shared budget.
    /// </summary>
    /// <param name="value">Chunk text to capture.</param>
    /// <param name="wasTruncated">Whether any portion of the chunk was omitted.</param>
    /// <returns>The captured chunk prefix.</returns>
    public string CapturePrefix(string value, out bool wasTruncated)
    {
        lock (_gate)
        {
            string captured = CommandOutputLimiter.TakeUtf8Prefix(value, _remainingBytes, out int usedBytes);
            _remainingBytes -= usedBytes;
            wasTruncated = captured.Length < value.Length;
            return captured;
        }
    }
}
