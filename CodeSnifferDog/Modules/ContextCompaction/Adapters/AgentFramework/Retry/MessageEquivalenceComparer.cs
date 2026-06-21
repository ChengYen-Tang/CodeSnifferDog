using Microsoft.Extensions.AI;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

internal static class MessageEquivalenceComparer
{
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

    private static bool ContentsAreEquivalent(IList<AIContent> left, IList<AIContent> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
            if (!string.Equals(CanonicalizeContent(left[index]), CanonicalizeContent(right[index]), StringComparison.Ordinal))
                return false;

        return true;
    }

    private static bool AdditionalPropertiesAreEquivalent(
        AdditionalPropertiesDictionary? left,
        AdditionalPropertiesDictionary? right) =>
        CanonicalizeAdditionalProperties(left) == CanonicalizeAdditionalProperties(right);

    private static string CanonicalizeContent(AIContent content)
    {
        IEnumerable<string> entries = content.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={CanonicalizeValue(property.GetValue(content))}");

        return $"{content.GetType().FullName}|{string.Join("|", entries)}";
    }

    private static string CanonicalizeAdditionalProperties(AdditionalPropertiesDictionary? properties)
    {
        if (properties is null || properties.Count == 0)
            return string.Empty;

        IEnumerable<string> entries = properties
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key}={CanonicalizeValue(pair.Value)}");

        return string.Join("|", entries);
    }

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
