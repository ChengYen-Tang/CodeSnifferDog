namespace CodeSnifferDog.Modules.Tools.Listing;

/// <summary>
/// Creates bounded text previews suitable for agent tool indexes.
/// </summary>
internal static class TextPreview
{
    /// <summary>
    /// Reduces text to a bounded preview while preserving that truncation occurred.
    /// </summary>
    public static string Create(string value, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 1);

        if (value.Length <= maximumLength)
            return value;

        int previewLength = maximumLength - 1;

        if (previewLength > 0
            && char.IsHighSurrogate(value[previewLength - 1])
            && char.IsLowSurrogate(value[previewLength]))
        {
            previewLength--;
        }

        return value[..previewLength] + "…";
    }
}
