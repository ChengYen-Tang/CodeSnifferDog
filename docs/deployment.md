# Deployment

## Publish

The repository includes a Windows single-file publish profile:

```powershell
dotnet publish `
  CodeSnifferDog.Server/CodeSnifferDog.Server/CodeSnifferDog.Server.csproj `
  -c Release `
  -p:PublishProfile=FolderProfile
```

The profile publishes a self-contained `win-x64` executable to `bin/Release/net10.0/publish/`. Keep the generated `appsettings.json` beside the executable and replace its deployment-specific database and inference settings before starting the server.

For Linux or macOS, publish with the appropriate .NET runtime identifier and ensure the matching bundled `ripgrep` asset is included. The application hosts PowerShell 7 in-process through `Microsoft.PowerShell.SDK`; a separate PowerShell installation is not required by the application.

## Runtime data

- Logs are written under `<application-base>/logs/` and also sent to the console.
- Uploaded archives and extracted repositories are stored under `<application-base>/TemporaryStorage/`.
- Database migrations run automatically at startup.
- Generated reports are persisted with the project state and can be downloaded through the UI or API.

Back up the database and any report data that must survive a host replacement. Temporary extracted repositories can be treated as disposable according to the retention policy of the deployment.

## Production checklist

- Set a production SQL Server connection string and verify migrations can run with the deployment identity.
- Store provider credentials in a secret store or environment variables.
- Set provider model, endpoint, reasoning options, and `ExtraBody` fields only to values supported by the selected backend.
- Tune worker concurrency, timeouts, queue limits, and context-window settings for the provider and host capacity.
- Put the server behind TLS and an authentication/authorization boundary before exposing it outside a trusted network.
- Restrict upload size and temporary-storage permissions; uploaded archives are untrusted input.
- Configure log retention and monitor queue depth, failed projects, model-call latency, and storage growth.
