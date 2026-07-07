using System.Text;

namespace CodeSnifferDog.Modules.Estimation;

/// <summary>
/// Provides local coarse token estimates based on UTF-8 byte counts.
/// </summary>
internal static class TokenEstimator
{
    /// <summary>
    /// Defines the local UTF-8 byte-to-token ratio used for coarse estimates.
    /// </summary>
    private const int Utf8BytesPerToken = 4;

    /// <summary>
    /// Estimates the token cost of UTF-8 text.
    /// </summary>
    /// <param name="value">Text to estimate.</param>
    /// <returns>A coarse token estimate that is zero for empty text.</returns>
    public static int EstimateText(string? value)
    {
        int byteCount = GetUtf8ByteCount(value);
        return byteCount == 0 ? 0 : Math.Max(1, byteCount / Utf8BytesPerToken);
    }

    /// <summary>
    /// Estimates the token cost of a UTF-8 byte count.
    /// </summary>
    /// <param name="byteCount">Byte count to estimate.</param>
    /// <returns>A coarse token estimate that is zero for no bytes.</returns>
    public static int EstimateBytes(long byteCount)
    {
        if (byteCount <= 0)
            return 0;

        long estimatedTokens = Math.Max(1L, byteCount / Utf8BytesPerToken);
        return estimatedTokens > int.MaxValue ? int.MaxValue : (int)estimatedTokens;
    }

    /// <summary>
    /// Returns the UTF-8 byte count for text.
    /// </summary>
    /// <param name="value">Text to measure.</param>
    /// <returns>The UTF-8 byte count.</returns>
    public static int GetUtf8ByteCount(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);

    /// <summary>
    /// Converts a token budget to its coarse UTF-8 byte budget.
    /// </summary>
    /// <param name="tokens">Token budget.</param>
    /// <returns>The corresponding UTF-8 byte budget.</returns>
    public static int GetUtf8ByteBudget(int tokens) =>
        checked(tokens * Utf8BytesPerToken);
}
