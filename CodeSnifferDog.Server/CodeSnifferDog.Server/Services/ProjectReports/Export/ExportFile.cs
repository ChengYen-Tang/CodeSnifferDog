namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

/// <summary>
/// Represents one exported report file payload.
/// </summary>
/// <param name="Bytes">File bytes.</param>
/// <param name="ContentType">HTTP content type.</param>
/// <param name="FileName">Suggested download file name.</param>
internal sealed record ExportFile(
    byte[] Bytes,
    string ContentType,
    string FileName);
