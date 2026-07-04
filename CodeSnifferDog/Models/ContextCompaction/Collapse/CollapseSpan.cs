using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

/// <summary>
/// Describes one archived span of transcript messages represented by collapse projection messages.
/// </summary>
public abstract class CollapseSpan
{
    /// <summary>
    /// Gets the stable collapse span identifier.
    /// </summary>
    public required string CollapseId { get; init; }

    /// <summary>
    /// Gets the message identifier of the summary message emitted for the span.
    /// </summary>
    public required string SummaryMessageId { get; init; }

    /// <summary>
    /// Gets the message identifier of the projection message emitted for the span.
    /// </summary>
    public required string ProjectionMessageId { get; init; }

    /// <summary>
    /// Gets the message identifier of the continuity projection emitted for the span.
    /// </summary>
    public required string ContinuityProjectionMessageId { get; init; }

    /// <summary>
    /// Gets the summary text that represents the archived span.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the continuity state captured for the archived span.
    /// </summary>
    public required ContinuityState ContinuityState { get; init; }

    /// <summary>
    /// Gets the textual reason why the span was collapsed.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the index of the first archived message in the original transcript.
    /// </summary>
    public required int FirstArchivedMessageIndex { get; init; }

    /// <summary>
    /// Gets the identifier of the first archived message, when one exists.
    /// </summary>
    public string? FirstArchivedMessageId { get; init; }

    /// <summary>
    /// Gets the role of the first archived message.
    /// </summary>
    public required string FirstArchivedMessageRole { get; init; }

    /// <summary>
    /// Gets the text of the first archived message.
    /// </summary>
    public required string FirstArchivedMessageText { get; init; }

    /// <summary>
    /// Gets the index of the last archived message in the original transcript.
    /// </summary>
    public required int LastArchivedMessageIndex { get; init; }

    /// <summary>
    /// Gets the identifier of the last archived message, when one exists.
    /// </summary>
    public string? LastArchivedMessageId { get; init; }

    /// <summary>
    /// Gets the role of the last archived message.
    /// </summary>
    public required string LastArchivedMessageRole { get; init; }

    /// <summary>
    /// Gets the text of the last archived message.
    /// </summary>
    public required string LastArchivedMessageText { get; init; }

    /// <summary>
    /// Gets how many transcript messages were archived into the span.
    /// </summary>
    public required int ArchivedMessagesCount { get; init; }
}
