namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

internal sealed record ProjectUploadArtifact(
    string OriginalFileName,
    long FileSizeBytes,
    string StoredFilePath,
    string StoredZipRelativePath);
