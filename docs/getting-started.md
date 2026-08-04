# Getting started

This guide takes a local checkout from source to the first completed review.

## Prerequisites

- .NET 10 SDK
- SQL Server. Development settings use SQL Server LocalDB; other environments need a reachable SQL Server instance.
- An inference backend configured for the server. Supported provider families are OpenAI, Azure OpenAI, and OpenAI-compatible APIs.
- A runtime supported by the bundled `ripgrep` assets (Windows, Linux, and macOS x64/arm64 assets are included).

The shell tool runs PowerShell 7 in-process through `Microsoft.PowerShell.SDK`; a separate `pwsh` installation is not required by the application.

## Build and run

From the repository root:

```powershell
dotnet restore CodeSnifferDog.slnx
dotnet build CodeSnifferDog.slnx
dotnet test CodeSnifferDog.slnx
dotnet run --project CodeSnifferDog.Server/CodeSnifferDog.Server/CodeSnifferDog.Server.csproj --launch-profile http
```

Open the HTTP URL printed in the server console. The host and port come from the selected ASP.NET Core launch profile or deployment configuration. On startup, the server applies pending Entity Framework Core migrations. A database connection failure stops startup instead of leaving the schema incomplete.

## Run the first review

1. Open the home page and choose **New Project**.
2. Select or drop a `.zip` repository archive.
3. Select **Upload Project**. The project is queued and the browser navigates to its Agent Status page.
4. Follow the scan, planning, rule-review, verifier, and report-generation stages.
5. Open **Reports** when the project reaches `Completed` to preview or download Markdown reports.

Only `.zip` uploads are accepted. A project can be canceled while it is running or deleted from the project actions.

## Useful development commands

Run a specific test project:

```powershell
dotnet test CodeSnifferDog.Tests/CodeSnifferDog.Tests.csproj
```

Run the server with a different environment:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project CodeSnifferDog.Server/CodeSnifferDog.Server/CodeSnifferDog.Server.csproj
```

Keep API keys and database credentials out of source control. Use user secrets, environment variables, or a deployment secret store; see [Configuration](configuration.md).
