# 1) Repeated Work & Hot Paths

- Repeated parsing, serialization, reflection, model creation, dependency discovery, formatting, allocation, or expensive computation inside loops
- Repeated full scans where incremental, cached, indexed, narrowed, or precomputed lookup is available
- Work performed on every request, render, poll, retry, callback, message, or item when it could be done once
- Duplicate operations caused by nested loops, repeated workflow attempts, per-item service construction, or repeated conversion between representations
- Hot-path code that performs unnecessary allocation, conversion, sorting, filtering, string formatting, or data copying

# 2) I/O, Network & Database Efficiency

- N+1 database, file-system, network, API, subprocess, or external tool calls
- Large synchronous reads/writes, repeated file opens, repeated directory traversal, repeated archive operations, or repeated process launches
- Missing batching, pagination, streaming, throttling, caching, indexing, or deduplication for large inputs
- Loading entire datasets, files, responses, reports, logs, or result sets when only a subset is needed
- Expensive polling or refresh loops that run when no useful work is available

# 3) Async, Blocking & Concurrency Costs

- Blocking calls in async flows, sync-over-async, thread starvation risks, or unnecessary task waiting
- CPU-heavy work running on latency-sensitive request, UI, event-dispatch, lock-held, or callback paths
- Excessive parallelism, unbounded fan-out, unbounded queues, or missing backpressure
- Serial execution where independent expensive work should be bounded and parallelized
- Independent CPU-bound or I/O-bound work that could safely use bounded parallelism but is processed serially despite available CPU cores or I/O capacity
- Parallel work that uses an unsafe degree of parallelism, causes oversubscription, amplifies contention, exhausts shared resources, or ignores cancellation/backpressure
- Lock contention, overly broad critical sections, atomic hot spots, or unnecessary synchronization around slow operations

# 4) Algorithmic Complexity & Data Structures

- Algorithms with avoidable poor time complexity, such as nested scans, repeated linear lookup, avoidable O(n^2) or worse behavior, or exponential search
- Data structures that do not match the access pattern, such as list scans where a map, set, index, heap, trie, cache, or precomputed lookup would avoid repeated work
- Sorting, grouping, joining, deduplicating, filtering, or searching done repeatedly instead of once at the right boundary
- Recursion, backtracking, graph traversal, dependency traversal, dynamic programming, or combinatorial logic without clear pruning, memoization, visited tracking, or complexity control
- Performance-sensitive logic without documented or enforced input-size assumptions
- Improvements that materially reduce asymptotic complexity, not just constant-factor micro-optimizations

# 5) Memory, Buffering & Data Volume

- Unbounded collection growth, buffering, caching, queues, logs, transcripts, diagnostics, or accumulated state
- Large object allocation or copying where streaming, slicing, pooling, incremental processing, or bounded buffers are expected
- Holding large intermediate results longer than needed
- Repeated string concatenation, format expansion, encoding conversion, or large text transformations in loops
- Memory use that scales with repository size, project count, rule count, task count, file count, issue count, report size, or concurrent users without an explicit bound

# 6) Size, Arithmetic & Layout Calculations

- Size, offset, capacity, page, chunk, or buffer calculations that can overflow, truncate, wrap, or allocate much more than intended
- Mixed units such as bytes vs characters, encoded vs decoded length, items vs pages, rows vs batches, or signed vs unsigned values
- Negative, zero, maximum, or boundary values that trigger huge allocations, infinite loops, excessive retries, or full scans
- Narrowing conversions or precision loss in counts, durations, offsets, sizes, percentages, or rate calculations
- Layout or ABI assumptions that cause repeated copying, marshaling, padding, conversion, or compatibility work

# 7) Runtime, OS & Low-Latency Costs

- Hot paths or low-latency paths that create excessive tasks, threads, timers, processes, system calls, context switches, allocations, locks, exceptions, reflection calls, dynamic dispatch, or runtime marshaling
- Language or framework conveniences that hide expensive work in tight loops, latency-sensitive paths, high-frequency callbacks, serialization paths, or allocation-sensitive code
- OS resource dispatch costs from frequent process launches, file opens, network connects, synchronization primitives, thread creation, wakeups, scheduler handoffs, or kernel transitions
- Garbage collection, reference counting, finalization, pinning, native interop, or memory pressure that can introduce latency spikes
- Logging, tracing, metrics, diagnostics, formatting, or stack capture on hot paths without sampling, level checks, or bounded cost
- Low-latency code that lacks clear allocation, blocking, lock, syscall, scheduling, or runtime-dispatch boundaries

# 8) Scaling Triggers

- Behavior that is acceptable for small projects but degrades for large repositories, many files, many rules, many tasks, many reports, many issues, or concurrent users
- Work that scales worse than expected for the domain, such as O(n^2) comparisons on large lists
- Timeout, cancellation, progress, or batching behavior that becomes ineffective under large workloads
- Performance-sensitive code paths without clear size limits, operation limits, or operational safeguards
- Formatting, logging, diagnostics, or report generation that becomes expensive when output is large
