namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

internal sealed record Artifact(
    string OriginalFileName,
    long FileSizeBytes,
    string StoredFilePath,
    string StoredZipRelativePath);
