namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

/// <summary>
/// Describes the stored uploaded zip artifact before the project is queued.
/// </summary>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="FileSizeBytes">Uploaded file size in bytes.</param>
/// <param name="StoredFilePath">Absolute stored file path.</param>
/// <param name="StoredZipRelativePath">Relative stored zip path persisted with the project record.</param>
internal sealed record Artifact(
    string OriginalFileName,
    long FileSizeBytes,
    string StoredFilePath,
    string StoredZipRelativePath);
