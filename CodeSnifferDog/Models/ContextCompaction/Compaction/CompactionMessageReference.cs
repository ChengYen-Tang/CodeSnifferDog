using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

public sealed class CompactionMessageReference
{
    public required int MessageIndex { get; init; }

    public string? MessageId { get; init; }

    public required ChatRole Role { get; init; }

    public required string Text { get; init; }
}
