namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

internal sealed record ProjectQueueRequest(
    Guid ProjectId,
    string OriginalFileName,
    long FileSizeBytes,
    string StoredZipRelativePath,
    DateTimeOffset NowUtc);
