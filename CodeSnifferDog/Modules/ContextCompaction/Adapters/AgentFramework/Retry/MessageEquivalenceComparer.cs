using Microsoft.Extensions.AI;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

/// <summary>
/// Compares chat transcripts by canonicalized content so retry logic can detect no-op compaction results.
/// </summary>
internal static class MessageEquivalenceComparer
{
    /// <summary>
    /// Determines whether two message lists are structurally equivalent for retry purposes.
    /// </summary>
    /// <param name="left">First message list.</param>
    /// <param name="right">Second message list.</param>
    /// <returns><see langword="true" /> when both message lists serialize to the same canonical structure.</returns>
    public static bool AreEquivalent(
        IReadOnlyList<ChatMessage> left,
        IReadOnlyList<ChatMessage> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
            if (left[index].Role != right[index].Role ||
                !string.Equals(left[index].Text, right[index].Text, StringComparison.Ordinal) ||
                !ContentsAreEquivalent(left[index].Contents, right[index].Contents) ||
                !AdditionalPropertiesAreEquivalent(left[index].AdditionalProperties, right[index].AdditionalProperties))
                return false;

        return true;
    }

    /// <summary>
    /// Compares content payload lists by canonicalized property values.
    /// </summary>
    /// <param name="left">First content list.</param>
    /// <param name="right">Second content list.</param>
    /// <returns><see langword="true" /> when the lists contain equivalent content payloads in the same order.</returns>
    private static bool ContentsAreEquivalent(IList<AIContent> left, IList<AIContent> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
            if (!string.Equals(CanonicalizeContent(left[index]), CanonicalizeContent(right[index]), StringComparison.Ordinal))
                return false;

        return true;
    }

    /// <summary>
    /// Compares additional-properties dictionaries after canonicalization.
    /// </summary>
    /// <param name="left">First metadata dictionary.</param>
    /// <param name="right">Second metadata dictionary.</param>
    /// <returns><see langword="true" /> when the canonicalized metadata strings match.</returns>
    private static bool AdditionalPropertiesAreEquivalent(
        AdditionalPropertiesDictionary? left,
        AdditionalPropertiesDictionary? right) =>
        CanonicalizeAdditionalProperties(left) == CanonicalizeAdditionalProperties(right);

    /// <summary>
    /// Canonicalizes one AI content payload by reflecting over its readable public properties.
    /// </summary>
    /// <param name="content">Content payload to canonicalize.</param>
    /// <returns>A stable string representation used for equivalence comparison.</returns>
    private static string CanonicalizeContent(AIContent content)
    {
        IEnumerable<string> entries = content.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={CanonicalizeValue(property.GetValue(content))}");

        return $"{content.GetType().FullName}|{string.Join("|", entries)}";
    }

    /// <summary>
    /// Canonicalizes additional-properties metadata into a stable key-sorted string.
    /// </summary>
    /// <param name="properties">Metadata dictionary to canonicalize.</param>
    /// <returns>A stable string representation of the metadata.</returns>
    private static string CanonicalizeAdditionalProperties(AdditionalPropertiesDictionary? properties)
    {
        if (properties is null || properties.Count == 0)
            return string.Empty;

        IEnumerable<string> entries = properties
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key}={CanonicalizeValue(pair.Value)}");

        return string.Join("|", entries);
    }

    /// <summary>
    /// Canonicalizes one value for structural comparison, including nested dictionaries and enumerables.
    /// </summary>
    /// <param name="value">Value to canonicalize.</param>
    /// <returns>A stable string representation of <paramref name="value" />.</returns>
    private static string CanonicalizeValue(object? value)
    {
        if (value is null)
            return "<null>";

        if (value is string text)
            return text;

        if (value is bool boolean)
            return boolean ? "true" : "false";

        if (value is Enum enumValue)
            return enumValue.ToString();

        if (value is IDictionary dictionary)
        {
            List<string> entries = [];

            foreach (DictionaryEntry entry in dictionary)
                entries.Add($"{entry.Key}={CanonicalizeValue(entry.Value)}");

            return string.Join(",", entries.OrderBy(static item => item, StringComparer.Ordinal));
        }

        if (value is IEnumerable enumerable)
        {
            List<string> items = [];

            foreach (object? item in enumerable)
                items.Add(CanonicalizeValue(item));

            return $"[{string.Join(",", items)}]";
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }
}
