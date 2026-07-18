using CodeSnifferDog.Models.Common.Tools;
using System.Text;
using SharedTokenEstimator = CodeSnifferDog.Modules.Estimation.TokenEstimator;

namespace CodeSnifferDog.Modules.Tools.Output;

/// <summary>
/// Applies a hard token cap to command output before it is returned to the model.
/// </summary>
internal static class CommandOutputLimiter
{
    /// <summary>
    /// Defines the maximum combined stdout/stderr payload returned to the model.
    /// </summary>
    public const int MaxCombinedOutputTokens = 12_000;

    /// <summary>
    /// Gets the UTF-8 byte budget corresponding to <see cref="MaxCombinedOutputTokens"/>.
    /// </summary>
    internal static readonly int MaxCombinedOutputBytes =
        SharedTokenEstimator.GetUtf8ByteBudget(MaxCombinedOutputTokens);

    /// <summary>
    /// Limits the combined standard output and standard error payload.
    /// </summary>
    /// <param name="result">Original command result.</param>
    /// <returns>The original result, or a capped result with a warning.</returns>
    public static CommandExecutionResult Limit(CommandExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        int standardOutputBytes = SharedTokenEstimator.GetUtf8ByteCount(result.StandardOutput);
        int standardErrorBytes = SharedTokenEstimator.GetUtf8ByteCount(result.StandardError);
        int originalBytes = standardOutputBytes + standardErrorBytes;

        if (originalBytes <= MaxCombinedOutputBytes)
            return result;

        int originalLines = CountLines(result.StandardOutput) + CountLines(result.StandardError);
        return CreateTruncatedResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            originalLines,
            originalBytes);
    }

    /// <summary>
    /// Creates a capped command result that includes a clear truncation warning.
    /// </summary>
    /// <param name="exitCode">Original process exit code.</param>
    /// <param name="standardOutput">Captured standard output text.</param>
    /// <param name="standardError">Captured standard error text.</param>
    /// <param name="originalLines">Original combined output line count.</param>
    /// <param name="originalBytes">Original combined output UTF-8 byte count.</param>
    /// <returns>The capped command result.</returns>
    internal static CommandExecutionResult CreateTruncatedResult(
        int exitCode,
        string standardOutput,
        string standardError,
        int originalLines,
        long originalBytes,
        bool pipelineWasStoppedEarly = false)
    {
        string outputMetadata = pipelineWasStoppedEarly
            ? $"Output observed before the pipeline was stopped. Lines: {originalLines}, bytes: {originalBytes}."
            : $"Original lines: {originalLines}, original bytes: {originalBytes}.";
        string warning =
            $"Warning: command output was too large and was truncated. {outputMetadata} Do not retry the same large-output command with Shell. Do not use unbounded recursive directory listings such as Get-ChildItem -Recurse. Use Ripgrep to narrow files, symbols, or line numbers, or use bounded file listings with explicit filters/limits. Then use ReadFileRange with smaller offsetLine/limitLines to read file content. The file-reading tool is ReadFileRange.";
        int remainingBytes = Math.Max(
            0,
            MaxCombinedOutputBytes -
            SharedTokenEstimator.GetUtf8ByteCount(warning) -
            SharedTokenEstimator.GetUtf8ByteCount(Environment.NewLine));

        string limitedStandardOutput = TakeUtf8Prefix(standardOutput, remainingBytes, out int usedOutputBytes);
        remainingBytes = Math.Max(0, remainingBytes - usedOutputBytes);
        string limitedStandardError = warning + Environment.NewLine + TakeUtf8Prefix(standardError, remainingBytes, out _);

        return new CommandExecutionResult
        {
            ExitCode = exitCode,
            StandardOutput = limitedStandardOutput,
            StandardError = limitedStandardError,
        };
    }

    /// <summary>
    /// Counts line breaks in output text while treating a final trailing newline as line termination.
    /// </summary>
    /// <param name="value">Text whose lines should be counted.</param>
    /// <returns>The number of logical lines in the text.</returns>
    internal static int CountLines(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int lines = 1;

        foreach (char character in value)
        {
            if (character == '\n')
                lines++;
        }

        return value[^1] == '\n' ? lines - 1 : lines;
    }

    /// <summary>
    /// Returns the longest prefix that fits within a UTF-8 byte budget without splitting a rune.
    /// </summary>
    /// <param name="value">Text to trim.</param>
    /// <param name="maxBytes">Maximum UTF-8 bytes to keep.</param>
    /// <param name="usedBytes">The UTF-8 byte count of the returned prefix.</param>
    /// <returns>The prefix that fits within the byte budget.</returns>
    internal static string TakeUtf8Prefix(string value, int maxBytes, out int usedBytes)
    {
        if (string.IsNullOrEmpty(value) || maxBytes <= 0)
        {
            usedBytes = 0;
            return string.Empty;
        }

        int byteCount = 0;
        int length = 0;

        foreach (Rune rune in value.EnumerateRunes())
        {
            int runeBytes = rune.Utf8SequenceLength;

            if (byteCount + runeBytes > maxBytes)
                break;

            byteCount += runeBytes;
            length += rune.Utf16SequenceLength;
        }

        usedBytes = byteCount;
        return length == value.Length ? value : value[..length];
    }
}
