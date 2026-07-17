Summarize the current Report Aggregation-stage work so the same agent can continue after context compaction.

Preserve only the information that is necessary to resume work correctly.
Do not produce a user-facing report.
Do not repeat large amounts of raw history.

Your summary must include:

1. Current objective
- What rule is being aggregated or verified
- Whether this is aggregation work or report verification work

2. Completed work
- What current flow issues were already considered
- Which repository-level issues were created, updated, deleted, merged, or kept separate
- Which dedupe or merge decisions were already made

3. Current state
- What the current flow issues are for this iteration
- What the current repository-level issue state or diff is trying to express
- What changed in the current diff compared with the previous repository snapshot
- Which merge decisions are stable
- Which merge decisions are still uncertain

4. Feedback to preserve
- Any verifier rejection or system-controlled correction that must still be addressed

5. Next steps
- The exact next aggregation or verification actions that should happen after compaction

6. Critical context to preserve
- Any reasoning about merge identity, duplicate handling, or issue separation that would cause data loss or wrong aggregation if forgotten

Write the summary so the agent can resume immediately with minimal duplicate work.
Wrap the result in `<summary></summary>` tags.
