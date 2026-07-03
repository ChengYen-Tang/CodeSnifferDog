using Microsoft.Extensions.AI;
using System.Text;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class ContinuityStateBuilder
{
    private static readonly string[] s_knownSectionHeaders =
    [
        "Current objective",
        "Completed work",
        "Next steps",
        "Critical context",
        "Constraints",
        "Open questions",
    ];

    public static ContinuityState Build(string normalizedSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedSummary);

        Dictionary<string, string> sections = ParseSections(normalizedSummary);

        return new ContinuityState
        {
            CurrentObjective = GetSection(sections, "Current objective"),
            CompletedWork = GetSection(sections, "Completed work"),
            NextSteps = GetSection(sections, "Next steps"),
            CriticalContext = BuildCriticalContext(sections),
        };
    }

    public static ChatMessage CreateMessage(
        ContinuityState continuityState,
        CompactionReason reason)
    {
        ArgumentNullException.ThrowIfNull(continuityState);

        ChatMessage message = new(
            ChatRole.System,
            CreateText(continuityState))
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.ContinuityArtifactKind,
                [CompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
                [CompactionArtifactMetadata.ContinuityCurrentObjectiveKey] = continuityState.CurrentObjective,
                [CompactionArtifactMetadata.ContinuityCompletedWorkKey] = continuityState.CompletedWork,
                [CompactionArtifactMetadata.ContinuityNextStepsKey] = continuityState.NextSteps,
                [CompactionArtifactMetadata.ContinuityCriticalContextKey] = continuityState.CriticalContext,
            },
        };

        return message;
    }

    public static ChatMessage CreateProjectionMessage(
        ContinuityState continuityState,
        string messageId,
        string collapseId,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(continuityState);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(collapseId);

        ChatMessage message = new(
            ChatRole.System,
            $"Collapsed continuity state {collapseId}{Environment.NewLine}{Environment.NewLine}{CreateText(continuityState)}")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.ContinuityArtifactKind,
                [CompactionArtifactMetadata.MessageIdentityKey] = messageId,
                [CompactionArtifactMetadata.CollapseCommitIdKey] = collapseId,
                [CompactionArtifactMetadata.CompactionReasonKey] = reason,
                [CompactionArtifactMetadata.ContinuityCurrentObjectiveKey] = continuityState.CurrentObjective,
                [CompactionArtifactMetadata.ContinuityCompletedWorkKey] = continuityState.CompletedWork,
                [CompactionArtifactMetadata.ContinuityNextStepsKey] = continuityState.NextSteps,
                [CompactionArtifactMetadata.ContinuityCriticalContextKey] = continuityState.CriticalContext,
            },
        };

        return message;
    }

    private static Dictionary<string, string> ParseSections(string normalizedSummary)
    {
        Dictionary<string, StringBuilder> builders = [];
        string? currentHeader = null;

        foreach (string rawLine in normalizedSummary.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            string? normalizedHeader = NormalizeHeader(line);
            if (normalizedHeader is not null)
            {
                currentHeader = normalizedHeader;
                if (!builders.ContainsKey(currentHeader))
                    builders[currentHeader] = new StringBuilder();

                continue;
            }

            if (currentHeader is null)
                continue;

            if (builders[currentHeader].Length > 0)
                builders[currentHeader].AppendLine();

            builders[currentHeader].Append(line);
        }

        return builders.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToString().Trim(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeHeader(string line)
    {
        string candidate = line.Trim().TrimEnd(':');
        return s_knownSectionHeaders.FirstOrDefault(header =>
            string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSection(
        IReadOnlyDictionary<string, string> sections,
        string sectionName) =>
        sections.TryGetValue(sectionName, out string? value)
            ? value
            : string.Empty;

    private static string BuildCriticalContext(IReadOnlyDictionary<string, string> sections)
    {
        List<string> parts = [];

        AppendNamedSection(parts, "Critical context", GetSection(sections, "Critical context"));
        AppendNamedSection(parts, "Constraints", GetSection(sections, "Constraints"));
        AppendNamedSection(parts, "Open questions", GetSection(sections, "Open questions"));

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static void AppendNamedSection(
        List<string> parts,
        string sectionName,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        parts.Add($"{sectionName}:{Environment.NewLine}{content}");
    }

    private static string CreateText(ContinuityState continuityState) =>
        $$"""
        Operational continuity state

        Current objective:
        {{continuityState.CurrentObjective}}

        Completed work:
        {{continuityState.CompletedWork}}

        Next steps:
        {{continuityState.NextSteps}}

        Critical context:
        {{continuityState.CriticalContext}}
        """;
}
