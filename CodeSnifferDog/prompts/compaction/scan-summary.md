Summarize the current Scan-stage work so the same agent can continue after context compaction.

Preserve only the information that is necessary to resume work correctly.
Do not produce a user-facing report.
Do not repeat large amounts of raw history.

Your summary must include:

1. Current objective
- What repository is being scanned
- Whether this is scan or scan verification work

2. Completed work
- Which repository areas were already inspected
- Which project units were already identified
- Which scan results were added, removed, or kept

3. Current state
- What the current scan result looks like
- What parts of the repository structure are still uncertain
- Any suspicious project unit that still needs confirmation

4. Feedback to preserve
- Any verifier rejection or system-controlled correction that must still be addressed

5. Next steps
- The exact next scan or verification actions that should happen after compaction

6. Critical context to preserve
- Any repository-specific fact that would cause duplicate work or wrong scan coverage if forgotten

Write the summary so the agent can resume immediately with minimal duplicate work.
Wrap the result in `<summary></summary>` tags.
