namespace CodeSnifferDog.Server.Services.ProjectReports.Export;

internal sealed record ProjectReportExportFile(
    byte[] Bytes,
    string ContentType,
    string FileName);
