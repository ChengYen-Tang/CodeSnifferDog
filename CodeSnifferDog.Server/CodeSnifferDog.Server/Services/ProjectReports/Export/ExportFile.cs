namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

internal sealed record ExportFile(
    byte[] Bytes,
    string ContentType,
    string FileName);
