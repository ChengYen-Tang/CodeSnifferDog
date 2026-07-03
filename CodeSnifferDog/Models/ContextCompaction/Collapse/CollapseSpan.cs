using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

public abstract class CollapseSpan
{
    public required string CollapseId { get; init; }

    public required string SummaryMessageId { get; init; }

    public required string ProjectionMessageId { get; init; }

    public required string ContinuityProjectionMessageId { get; init; }

    public required string Summary { get; init; }

    public required ContinuityState ContinuityState { get; init; }

    public required string Reason { get; init; }

    public required int FirstArchivedMessageIndex { get; init; }

    public string? FirstArchivedMessageId { get; init; }

    public required string FirstArchivedMessageRole { get; init; }

    public required string FirstArchivedMessageText { get; init; }

    public required int LastArchivedMessageIndex { get; init; }

    public string? LastArchivedMessageId { get; init; }

    public required string LastArchivedMessageRole { get; init; }

    public required string LastArchivedMessageText { get; init; }

    public required int ArchivedMessagesCount { get; init; }
}
