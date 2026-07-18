using CodeSnifferDog.Models.Common.Tools;
using CodeSnifferDog.Modules.Estimation;
using CodeSnifferDog.Modules.Tools.Output;
using System.Text;

namespace CodeSnifferDog.Modules.Tools.Shell;

/// <summary>
/// Captures PowerShell output incrementally within the common command-output budget.
/// </summary>
internal sealed class PowerShellOutputCapture
{
    /// <summary>
    /// Tracks the shared output budget across both PowerShell streams.
    /// </summary>
    private readonly CommandOutputCaptureBudget _budget = new(CommandOutputLimiter.MaxCombinedOutputBytes);

    /// <summary>
    /// Captures standard output independently from the error stream.
    /// </summary>
    private readonly StreamCapture _standardOutput = new();

    /// <summary>
    /// Captures standard error independently from standard output.
    /// </summary>
    private readonly StreamCapture _standardError = new();

    /// <summary>
    /// Ensures that a budget overflow requests one pipeline stop.
    /// </summary>
    private int _limitExceeded;

    /// <summary>
    /// Raised once when the bounded capture omits output.
    /// </summary>
    public event Action? LimitExceeded;

    /// <summary>
    /// Gets whether a stream exceeded the combined output budget.
    /// </summary>
    public bool WasTruncated => Volatile.Read(ref _limitExceeded) != 0;

    /// <summary>
    /// Captures one formatted PowerShell output line.
    /// </summary>
    /// <param name="value">Formatted output produced by <c>Out-String -Stream</c>.</param>
    public void CaptureOutput(object? value)
        => Capture(_standardOutput, value?.ToString() ?? string.Empty, appendLineTerminator: true);

    /// <summary>
    /// Captures one PowerShell error record.
    /// </summary>
    /// <param name="value">Error record text.</param>
    public void CaptureError(object? value)
        => Capture(_standardError, value?.ToString() ?? string.Empty, appendLineTerminator: true);

    /// <summary>
    /// Captures a terminating exception that was not represented by a stream record.
    /// </summary>
    /// <param name="message">Terminating exception text.</param>
    public void CaptureTerminatingError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Capture(_standardError, message, appendLineTerminator: true);
    }

    /// <summary>
    /// Creates the command result using the common truncation contract when capture was bounded.
    /// </summary>
    /// <param name="exitCode">Normalized PowerShell exit code.</param>
    /// <returns>The captured command result.</returns>
    public CommandExecutionResult CreateResult(int exitCode, bool pipelineWasStoppedEarly = false)
    {
        StreamCaptureSnapshot standardOutput = _standardOutput.CreateSnapshot();
        StreamCaptureSnapshot standardError = _standardError.CreateSnapshot();

        if (WasTruncated || standardOutput.WasTruncated || standardError.WasTruncated)
        {
            return CommandOutputLimiter.CreateTruncatedResult(
                exitCode,
                standardOutput.CapturedText,
                standardError.CapturedText,
                standardOutput.OriginalLines + standardError.OriginalLines,
                standardOutput.OriginalBytes + standardError.OriginalBytes,
                pipelineWasStoppedEarly);
        }

        return new CommandExecutionResult
        {
            ExitCode = exitCode,
            StandardOutput = standardOutput.CapturedText,
            StandardError = standardError.CapturedText,
        };
    }

    /// <summary>
    /// Captures text in one stream and requests a stop when the shared limit is exceeded.
    /// </summary>
    /// <param name="stream">Target stream capture.</param>
    /// <param name="value">Text to capture.</param>
    /// <param name="appendLineTerminator">Whether PowerShell emitted one logical output line.</param>
    private void Capture(StreamCapture stream, string value, bool appendLineTerminator)
    {
        bool wasTruncated = stream.Capture(value, _budget);

        if (appendLineTerminator)
            wasTruncated |= stream.Capture(Environment.NewLine, _budget);

        if (wasTruncated && Interlocked.Exchange(ref _limitExceeded, 1) == 0)
            LimitExceeded?.Invoke();
    }

    /// <summary>
    /// Stores bounded text and original-output statistics for one PowerShell stream.
    /// </summary>
    private sealed class StreamCapture
    {
        /// <summary>
        /// Synchronizes callbacks and result construction for this stream.
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// Stores text retained inside the shared byte budget.
        /// </summary>
        private readonly StringBuilder _capturedText = new();

        /// <summary>
        /// Stores the original UTF-8 byte count observed before the pipeline stopped.
        /// </summary>
        private long _originalBytes;

        /// <summary>
        /// Stores the number of line-feed characters observed.
        /// </summary>
        private int _newlineCount;

        /// <summary>
        /// Indicates that this stream observed text.
        /// </summary>
        private bool _hasContent;

        /// <summary>
        /// Indicates whether the last observed chunk ended with a line-feed.
        /// </summary>
        private bool _endsWithNewline;

        /// <summary>
        /// Indicates that this stream omitted content after the shared budget was exhausted.
        /// </summary>
        private bool _wasTruncated;

        /// <summary>
        /// Captures a text chunk while updating original-output statistics.
        /// </summary>
        /// <param name="chunk">Text emitted by the stream.</param>
        /// <param name="budget">Shared byte budget.</param>
        /// <returns>Whether any part of this chunk was omitted.</returns>
        public bool Capture(string chunk, CommandOutputCaptureBudget budget)
        {
            lock (_gate)
            {
                _originalBytes += TokenEstimator.GetUtf8ByteCount(chunk);
                _hasContent |= chunk.Length > 0;
                _endsWithNewline = chunk.EndsWith('\n');

                foreach (char character in chunk)
                {
                    if (character == '\n')
                        _newlineCount++;
                }

                string capturedChunk = budget.CapturePrefix(chunk, out bool wasTruncated);
                _capturedText.Append(capturedChunk);
                _wasTruncated |= wasTruncated;
                return wasTruncated;
            }
        }

        /// <summary>
        /// Creates an immutable snapshot after the command finishes.
        /// </summary>
        /// <returns>Captured text and original-output metadata.</returns>
        public StreamCaptureSnapshot CreateSnapshot()
        {
            lock (_gate)
            {
                int originalLines = _newlineCount + (_hasContent && !_endsWithNewline ? 1 : 0);
                return new StreamCaptureSnapshot(
                    _capturedText.ToString(),
                    _originalBytes,
                    originalLines,
                    _wasTruncated);
            }
        }
    }

    /// <summary>
    /// Represents a completed snapshot of one bounded PowerShell output stream.
    /// </summary>
    /// <param name="CapturedText">Text retained inside the combined budget.</param>
    /// <param name="OriginalBytes">UTF-8 bytes observed before the pipeline stopped.</param>
    /// <param name="OriginalLines">Logical line count observed before the pipeline stopped.</param>
    /// <param name="WasTruncated">Whether text was omitted from this stream.</param>
    private sealed record StreamCaptureSnapshot(
        string CapturedText,
        long OriginalBytes,
        int OriginalLines,
        bool WasTruncated);
}
