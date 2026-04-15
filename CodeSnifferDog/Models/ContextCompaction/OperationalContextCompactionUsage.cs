namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCompactionUsage
{
    public required long UsedTokens { get; init; }

    public long? ContextWindowTokens { get; init; }
}
