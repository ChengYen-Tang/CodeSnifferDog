# User guide

## Review workflow

CodeSnifferDog treats each uploaded repository as a project. The worker processes it through these stages:

1. **Queue** — the upload is stored and waits for an available worker.
2. **Scan** — the repository is inspected and its structure is collected.
3. **Project planning** — agents identify the review scope and work items.
4. **Rule review** — review agents inspect the code against the configured rule templates.
5. **Verification** — verifier agents check findings and required submissions.
6. **Report generation** — accepted findings are written as Markdown reports.

The user-facing project states are:

| State | Meaning |
| --- | --- |
| `Queued` | Waiting for a worker |
| `Reviewing` | The staged review is running |
| `Completed` | Reports are available |
| `Failed` | Processing stopped after an unrecoverable error |
| `Canceled` | Cancellation was requested and accepted |

## Pages

- `/` — upload a new project and view project navigation.
- `/agent-status?projectId={guid}` — watch agents, timeline entries, tool calls, retries, failures, and context-compaction events.
- `/reports/{guid}` — browse, preview, and download generated reports.

The sidebar and status pages refresh through SignalR. Snapshot and polling fallbacks allow the UI to recover when a live connection is interrupted.

## Agent Status

The Agent Status page groups activity by agent and attempt. A timeline can include model messages, tool calls and results, verifier decisions, retry information, errors, and a `Context compacted` event when the worker rewrites a long context before continuing.

Long-running tool calls can make the model appear idle while the worker waits for the tool result. The browser remains usable and the timeline is updated when the call completes or fails.

## Reports

The Reports page lists generated Markdown reports and summarizes finding severity. Select a report to preview it in the browser, or use the download action for one report. The project-level download action returns all reports as a ZIP bundle.

## Project actions

- Cancel a project while it is reviewing.
- Delete a project to remove its state and temporary artifacts.
- Return to **New Project** to upload another repository.

If an upload or review fails, inspect the Agent Status timeline and the server logs before retrying. Provider errors, unavailable databases, unsupported request fields, and tool timeouts are common causes.
