using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

internal static class SummaryContract
{
    private const string SummaryOpenTag = "<summary>";
    private const string SummaryCloseTag = "</summary>";

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

    public static string Normalize(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new OperationalContextCompactionException("Operational context compaction summary was empty.");

        int openTagIndex = summary.IndexOf(SummaryOpenTag, StringComparison.OrdinalIgnoreCase);
        int closeTagIndex = summary.IndexOf(SummaryCloseTag, StringComparison.OrdinalIgnoreCase);

        if (openTagIndex < 0 || closeTagIndex < 0 || closeTagIndex <= openTagIndex)
            throw new OperationalContextCompactionException("Operational context compaction summary did not contain a valid <summary> block.");

        int contentStartIndex = openTagIndex + SummaryOpenTag.Length;
        string normalizedSummary = summary[contentStartIndex..closeTagIndex].Trim();

        if (string.IsNullOrWhiteSpace(normalizedSummary))
            throw new OperationalContextCompactionException("Operational context compaction summary <summary> block was empty.");

        return normalizedSummary;
    }

    public static void Validate(string summary, OperationalContextCompactionOptions options)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new OperationalContextCompactionException("Operational context compaction summary was empty.");

        foreach (string requiredFragment in options.RequiredSummaryFragments)
        {
            if (string.IsNullOrWhiteSpace(requiredFragment))
                continue;

            if (!summary.Contains(requiredFragment, StringComparison.OrdinalIgnoreCase))
                throw new OperationalContextCompactionException(
                    $"Operational context compaction summary did not contain required fragment '{requiredFragment}'.");
        }
    }
}
