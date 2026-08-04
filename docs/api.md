# HTTP API

The server exposes project operations under `/api/projects`. Use the host and port printed by the server at startup; the examples below use `http://localhost:<port>` as a placeholder.

## Project endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/projects/` | Upload a `.zip` archive in multipart field `file` |
| `GET` | `/api/projects/` | List projects |
| `GET` | `/api/projects/sidebar` | Load the sidebar snapshot |
| `GET` | `/api/projects/{projectId}` | Get one project summary |
| `GET` | `/api/projects/{projectId}/agent-status` | Get an agent-status snapshot |
| `GET` | `/api/projects/{projectId}/agent-status/agents/{agentId}/history` | Get one agent's timeline history |
| `GET` | `/api/projects/{projectId}/reports` | List reports |
| `GET` | `/api/projects/{projectId}/reports/{reportId}` | Get report content |
| `GET` | `/api/projects/{projectId}/reports/{reportId}/download` | Download one Markdown report |
| `GET` | `/api/projects/{projectId}/reports/download` | Download all reports as a ZIP |
| `POST` | `/api/projects/{projectId}/cancel` | Request cancellation of a reviewing project |
| `DELETE` | `/api/projects/{projectId}` | Delete a project and temporary artifacts |

Identifiers are GUIDs. The server validates the uploaded archive and rejects unsupported file types.

## Upload example

```powershell
curl.exe -X POST `
  -F "file=@C:\path\to\repository.zip" `
  http://localhost:<port>/api/projects/
```

The upload response identifies the created project. Use that ID with the status and report routes.

## Live updates

The SignalR hub is available at `/hubs/projects`. The Blazor client uses it for project-list refreshes and agent-status timeline updates, with snapshot/polling fallbacks for recovery after a disconnected transport.

## Operational notes

The API is intended to run behind the application's trusted deployment boundary. Add authentication, authorization, TLS, request-size limits, and rate limiting before exposing it to an untrusted network.
