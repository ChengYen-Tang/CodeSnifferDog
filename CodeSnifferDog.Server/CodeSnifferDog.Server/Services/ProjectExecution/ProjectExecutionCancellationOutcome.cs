namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal readonly record struct ProjectExecutionCancellationOutcome(
    bool ShouldUpdateDatabase,
    bool ShouldDeleteUploadedZip,
    bool ShouldDeleteExtractedProject)
{
    public static ProjectExecutionCancellationOutcome UserCanceled { get; } = new(
        ShouldUpdateDatabase: true,
        ShouldDeleteUploadedZip: true,
        ShouldDeleteExtractedProject: true);

    public static ProjectExecutionCancellationOutcome PreserveForRecovery { get; } = new(
        ShouldUpdateDatabase: false,
        ShouldDeleteUploadedZip: false,
        ShouldDeleteExtractedProject: false);
}
