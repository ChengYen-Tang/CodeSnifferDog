namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

internal sealed record Request(
    Guid ProjectId,
    string OriginalFileName,
    long FileSizeBytes,
    string StoredZipRelativePath,
    DateTimeOffset NowUtc);
