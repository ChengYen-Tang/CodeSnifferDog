Summarize the current Project Planning-stage work so the same agent can continue after context compaction.

Preserve only the information that is necessary to resume work correctly.
Do not produce a user-facing report.
Do not repeat large amounts of raw history.

Your summary must include:

1. Current objective
- What `ScanProject` is being planned or verified
- Whether this is planning or planning verification work

2. Completed work
- Which files or areas of the project were already inspected
- Which task items were already created, removed, regrouped, or kept
- Which splitting decisions were already made

3. Current state
- What the current task-item layout looks like
- Which files are already covered by task items
- Which files or groupings are still uncertain

4. Feedback to preserve
- Any verifier rejection or system-controlled correction that must still be addressed

5. Next steps
- The exact next planning or verification actions that should happen after compaction

6. Critical context to preserve
- Any project structure fact that would cause duplicate work, wrong grouping, or missed coverage if forgotten

Write the summary so the agent can resume immediately with minimal duplicate work.
Wrap the result in `<summary></summary>` tags.
