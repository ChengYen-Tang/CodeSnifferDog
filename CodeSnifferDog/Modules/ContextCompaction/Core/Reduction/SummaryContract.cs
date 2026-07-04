using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Defines the prompt and validation rules for compaction summaries.
/// </summary>
internal static class SummaryContract
{
    private const string SummaryOpenTag = "<summary>";
    private const string SummaryCloseTag = "</summary>";

    /// <summary>
    /// Appends the XML-like summary envelope contract to the caller-provided prompt.
    /// </summary>
    /// <param name="summaryPrompt">Base prompt that explains what the summary must capture.</param>
    /// <returns>A prompt that requires the model to emit exactly one <c>&lt;summary&gt;</c> block.</returns>
    public static string BuildPrompt(string summaryPrompt) =>
        $"""
        {summaryPrompt}

        Summary contract:
        - Return text only.
        - Do not call tools.
        - Put your final answer inside a single {SummaryOpenTag}...{SummaryCloseTag} block.
        - The summary must retain enough continuity for the next agent turn to continue safely.
        - Do not output any content after the closing summary tag.
        """;

    /// <summary>
    /// Extracts and trims the contents of the required <c>&lt;summary&gt;</c> block.
    /// </summary>
    /// <param name="summary">Raw model output to normalize.</param>
    /// <returns>The trimmed summary text inside the required tag pair.</returns>
    /// <exception cref="CompactionException"><paramref name="summary" /> is empty, lacks a valid summary block, or contains an empty block.</exception>
    public static string Normalize(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new CompactionException("Operational context compaction summary was empty.");

        int openTagIndex = summary.IndexOf(SummaryOpenTag, StringComparison.OrdinalIgnoreCase);
        int closeTagIndex = summary.IndexOf(SummaryCloseTag, StringComparison.OrdinalIgnoreCase);

        if (openTagIndex < 0 || closeTagIndex < 0 || closeTagIndex <= openTagIndex)
            throw new CompactionException("Operational context compaction summary did not contain a valid <summary> block.");

        int contentStartIndex = openTagIndex + SummaryOpenTag.Length;
        string normalizedSummary = summary[contentStartIndex..closeTagIndex].Trim();

        if (string.IsNullOrWhiteSpace(normalizedSummary))
            throw new CompactionException("Operational context compaction summary <summary> block was empty.");

        return normalizedSummary;
    }

    /// <summary>
    /// Verifies that the normalized summary contains every required fragment configured by the caller.
    /// </summary>
    /// <param name="summary">Normalized summary text to validate.</param>
    /// <param name="options">Compaction options that declare the required summary fragments.</param>
    /// <exception cref="CompactionException"><paramref name="summary" /> is empty or omits a required fragment.</exception>
    public static void Validate(string summary, CompactionOptions options)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new CompactionException("Operational context compaction summary was empty.");

        foreach (string requiredFragment in options.RequiredSummaryFragments)
        {
            if (string.IsNullOrWhiteSpace(requiredFragment))
                continue;

            if (!summary.Contains(requiredFragment, StringComparison.OrdinalIgnoreCase))
                throw new CompactionException(
                    $"Operational context compaction summary did not contain required fragment '{requiredFragment}'.");
        }
    }
}
