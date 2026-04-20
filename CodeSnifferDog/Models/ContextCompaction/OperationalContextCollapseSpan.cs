namespace CodeSnifferDog.Models.ContextCompaction;

public abstract class OperationalContextCollapseSpan
{
    public required string CollapseId { get; init; }

    public required string SummaryMessageId { get; init; }

    public required string ProjectionMessageId { get; init; }

    public required string ContinuityProjectionMessageId { get; init; }

    public required string Summary { get; init; }

    public required OperationalContextContinuityState ContinuityState { get; init; }

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
