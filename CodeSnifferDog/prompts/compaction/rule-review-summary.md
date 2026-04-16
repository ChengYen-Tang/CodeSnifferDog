Summarize the current Rule Review-stage work so the same agent can continue after context compaction.

Preserve only the information that is necessary to resume work correctly.
Do not produce a user-facing report.
Do not repeat large amounts of raw history.

Your summary must include:

1. Current objective
- What rule is being applied
- What scope entry files were given
- Whether this is review work or review verification work

2. Work completed
- Which scope entry files were already inspected
- Which follow-up files were already inspected
- Which review strategy paths were already tried
- Which issues were already created, updated, or deleted
- Whether a no-issue conclusion exists

3. Current state
- What evidence currently exists
- What coverage is already achieved
- What cross-scope dependencies are already understood
- What parts of the investigation are still weak or incomplete

4. Feedback to preserve
- Any verifier rejection or system-controlled correction that must still be addressed

5. Next steps
- The exact next review or verification actions that should happen after compaction

6. Critical context to preserve
- Any finding, uncertainty, failed approach, or dependency path that would cause duplicate work or a wrong conclusion if forgotten

Write the summary so the agent can resume immediately with minimal duplicate work.
Wrap the result in `<summary></summary>` tags.
