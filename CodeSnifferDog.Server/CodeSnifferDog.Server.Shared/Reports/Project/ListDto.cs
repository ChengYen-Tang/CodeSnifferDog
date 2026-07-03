namespace CodeSnifferDog.Server.Shared.Reports.Project;

public sealed class ListDto
{
    public required string OriginalFileName { get; init; }

    public required IReadOnlyList<ListItemDto> Reports { get; init; }
}
