using Microsoft.Extensions.AI;
using System.Text;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Parses normalized summaries into structured continuity state and serializes that state back into system messages.
/// </summary>
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

    /// <summary>
    /// Parses the normalized summary into the continuity sections consumed by future agent turns.
    /// </summary>
    /// <param name="normalizedSummary">Summary text that already satisfies the compaction summary contract.</param>
    /// <returns>The structured continuity state extracted from the known summary sections.</returns>
    /// <exception cref="ArgumentException"><paramref name="normalizedSummary" /> is <see langword="null" />, empty, or whitespace.</exception>
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

    /// <summary>
    /// Creates the continuity-state system message emitted by a fresh compaction result.
    /// </summary>
    /// <param name="continuityState">Structured continuity state to serialize.</param>
    /// <param name="reason">Reason the continuity artifact was produced.</param>
    /// <returns>A system message whose metadata and text mirror the supplied continuity state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuityState" /> is <see langword="null" />.</exception>
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

    /// <summary>
    /// Creates the continuity-state system message used when projecting a previously committed collapse span.
    /// </summary>
    /// <param name="continuityState">Structured continuity state captured when the collapse span was created.</param>
    /// <param name="messageId">Stable identifier assigned to the projected continuity message.</param>
    /// <param name="collapseId">Identifier of the collapse commit being projected.</param>
    /// <param name="reason">Serialized reason attached to the projected continuity artifact.</param>
    /// <returns>A system message that can stand in for the archived transcript span during projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuityState" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="messageId" /> or <paramref name="collapseId" /> is <see langword="null" />, empty, or whitespace.</exception>
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

    /// <summary>
    /// Parses the known section headers from the normalized summary into a lookup table.
    /// </summary>
    /// <param name="normalizedSummary">Normalized summary text to parse.</param>
    /// <returns>The extracted section contents keyed by normalized header name.</returns>
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

    /// <summary>
    /// Maps one summary line to a known continuity section header when it matches the expected labels.
    /// </summary>
    /// <param name="line">Trimmed summary line to inspect.</param>
    /// <returns>The normalized header name, or <see langword="null" /> when the line is not a known section header.</returns>
    private static string? NormalizeHeader(string line)
    {
        string candidate = line.Trim().TrimEnd(':');
        return s_knownSectionHeaders.FirstOrDefault(header =>
            string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets one parsed section value, defaulting to the empty string when the section is absent.
    /// </summary>
    /// <param name="sections">Parsed summary sections.</param>
    /// <param name="sectionName">Canonical section name to retrieve.</param>
    /// <returns>The parsed section content, or the empty string when the section is absent.</returns>
    private static string GetSection(
        IReadOnlyDictionary<string, string> sections,
        string sectionName) =>
        sections.TryGetValue(sectionName, out string? value)
            ? value
            : string.Empty;

    /// <summary>
    /// Combines the critical-context family of sections into the serialized continuity payload.
    /// </summary>
    /// <param name="sections">Parsed summary sections.</param>
    /// <returns>The merged critical-context text block.</returns>
    private static string BuildCriticalContext(IReadOnlyDictionary<string, string> sections)
    {
        List<string> parts = [];

        AppendNamedSection(parts, "Critical context", GetSection(sections, "Critical context"));
        AppendNamedSection(parts, "Constraints", GetSection(sections, "Constraints"));
        AppendNamedSection(parts, "Open questions", GetSection(sections, "Open questions"));

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>
    /// Appends one named section to the serialized continuity payload when it contains content.
    /// </summary>
    /// <param name="parts">Collected serialized section fragments.</param>
    /// <param name="sectionName">Display name of the section.</param>
    /// <param name="content">Section content to append.</param>
    private static void AppendNamedSection(
        List<string> parts,
        string sectionName,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        parts.Add($"{sectionName}:{Environment.NewLine}{content}");
    }

    /// <summary>
    /// Serializes continuity state into the standard system-message text format.
    /// </summary>
    /// <param name="continuityState">Structured continuity state to serialize.</param>
    /// <returns>The formatted continuity-state text.</returns>
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
