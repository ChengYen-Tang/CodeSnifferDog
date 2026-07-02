namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

internal readonly record struct Outcome(
    bool ShouldUpdateDatabase,
    bool ShouldDeleteUploadedZip,
    bool ShouldDeleteExtractedProject)
{
    public static Outcome UserCanceled { get; } = new(
        ShouldUpdateDatabase: true,
        ShouldDeleteUploadedZip: true,
        ShouldDeleteExtractedProject: true);

    public static Outcome PreserveForRecovery { get; } = new(
        ShouldUpdateDatabase: false,
        ShouldDeleteUploadedZip: false,
        ShouldDeleteExtractedProject: false);
}
