# Deployment

## Publish

The repository includes a cross-platform single-file publish profile. Pass the
target RID through `ReleaseRuntimeIdentifier`; do not pass `-r`/`--runtime`
globally, because the server references a Blazor WebAssembly client and a
global server RID is not a valid client runtime package.

```powershell
dotnet publish `
  CodeSnifferDog.Server/CodeSnifferDog.Server/CodeSnifferDog.Server.csproj `
  -c Release `
  -p:PublishProfile=ReleaseSingleFile `
  -p:ReleaseRuntimeIdentifier=win-x64
```

The profile publishes a self-contained single-file executable. Use
`win-x64`, `linux-x64`, `osx-arm64`, or `osx-x64` for the target platform. Keep
the generated `appsettings.json` beside the executable and replace its
deployment-specific database and inference settings before starting the
server.

The application hosts PowerShell 7 in-process through
`Microsoft.PowerShell.SDK`; a separate PowerShell installation is not required
by the application. The matching bundled `ripgrep` assets are included in the
publish output.

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
