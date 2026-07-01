# 1) Error Handling & Failure Paths

- Expected failures not handled, translated, retried, surfaced, or persisted correctly
- Exceptions, error codes, invalid handles, null returns, failed allocations, failed I/O, failed system calls, or failed external calls ignored or checked too late
- Partial success that leaves inconsistent state, duplicate work, leaked resources, missing cleanup, or misleading status
- Failure paths that notify users or callers before durable state has actually changed
- Fallback behavior that hides the real failure or continues with unsafe assumptions

# 2) Cancellation, Timeout & Retry

- Long-running work without cancellation propagation or timeout limits
- Cancellation that stops parent work but leaves child tasks, processes, threads, locks, files, subscriptions, handles, or temporary resources alive
- Retry behavior that is not idempotent, repeats side effects, reuses invalid state, or corrupts state
- Timeout handling that loses diagnostic context or leaves workflow state incomplete
- Race between cancellation, completion, retry, cleanup, notification, and persistence

# 3) Resource Lifecycle & Ownership

- Files, streams, processes, sockets, database contexts, subscriptions, timers, locks, temporary directories, handles, memory buffers, or native resources not released reliably
- Resources created before validation or state changes without cleanup on failure
- Cleanup that can fail silently while leaving future runs blocked
- Resource ownership unclear across modules, callbacks, background workers, async boundaries, interop boundaries, or object lifetime transitions
- Dispose, close, kill, unlock, unsubscribe, free, rollback, reset, or release missing from error and cancellation paths

# 4) Memory, Object State & Lifetime

- Null, dangling, stale, disposed, closed, moved-from, or otherwise invalid object/reference/handle access
- Use-after-free, double release, invalid release, use-after-dispose, use-after-close, or use-after-reset
- Returning or storing references, spans, slices, views, iterators, handles, or callbacks whose owner may expire
- Partially initialized objects, uninitialized values, default values used as valid state, or objects used before construction/initialization completes
- Objects used after teardown begins, after cancellation, after reset, after retry restore, or after ownership transfer
- Manual memory, unsafe buffers, native interop, or binary structures modified with generic copy/clear operations that bypass invariants

# 5) Bounds, Arithmetic & Data Validity

- Out-of-bounds access on arrays, strings, buffers, spans, slices, containers, result sets, pages, or collections
- Off-by-one errors, negative indexes, empty collection assumptions, missing length checks, or invalid range slicing
- Integer overflow, underflow, truncation, wraparound, divide-by-zero, invalid shifts, or bad modulo in size, offset, count, duration, or capacity calculations
- Mixed signed/unsigned, byte/character, encoded/decoded, local/UTC, or unit mismatch leading to invalid values
- Invalid casts, type narrowing, alignment/layout assumptions, or schema mismatches that can produce corrupt state

# 6) State Consistency & Recovery

- Non-atomic state transitions, stale reads, lost updates, duplicate submissions, or inconsistent status
- Restart or recovery path that cannot distinguish queued, running, failed, canceled, interrupted, retried, or completed work
- Persisted state updated in an order that can expose impossible intermediate states
- In-memory state that can diverge from persisted state after retry, reset, crash, cancellation, or concurrent execution
- Missing idempotency for operations that can be repeated by retry, refresh, worker restart, reconnect, or user action

# 7) Concurrency & Ordering

- Shared mutable state accessed without appropriate synchronization or ownership
- Race conditions between workers, requests, threads, background jobs, UI refresh, notifications, cleanup, retries, or finalization
- Deadlock risk from lock ordering, blocking waits, callbacks under locks, or mixed sync/async locking
- Assumptions about event ordering, memory visibility, message delivery, task completion, or callback order that are not guaranteed
- Concurrent readers/writers that can observe partially updated data or invalid lifetime transitions
- Non-thread-safe APIs, objects, handles, buffers, or clients used concurrently

# 8) Exception & Boundary Behavior

- Exceptions crossing boundaries that cannot safely receive them, such as thread entry points, callbacks, plugin boundaries, native/managed interop, process boundaries, destructors/finalizers, or cleanup hooks
- Cleanup or finalization code that throws and masks the original failure
- Error state ignored until it causes null, invalid handle, invalid state, or corrupt data access later
- Boundary declarations, schemas, calling conventions, data layouts, or compatibility assumptions that differ between producer and consumer
- Inconsistent behavior between debug/release, platform variants, runtime versions, or feature flags
