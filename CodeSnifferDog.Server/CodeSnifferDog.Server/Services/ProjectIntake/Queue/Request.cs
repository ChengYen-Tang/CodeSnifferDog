namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

/// <summary>
/// Request payload persisted when a newly uploaded project is queued.
/// </summary>
/// <param name="ProjectId">Project identifier.</param>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="FileSizeBytes">Uploaded file size in bytes.</param>
/// <param name="StoredZipRelativePath">Relative path of the stored uploaded zip.</param>
/// <param name="NowUtc">Timestamp used for created/updated/queued fields.</param>
internal sealed record Request(
    Guid ProjectId,
    string OriginalFileName,
    long FileSizeBytes,
    string StoredZipRelativePath,
    DateTimeOffset NowUtc);
