# 1) Review Scope, Evidence & Applicability

- Focus on observed or plausibly hot paths: complexity, repeated work, I/O, networking, allocation, serialization, blocking, and resource contention. Do not treat every code path as performance-critical by default.
- Assess recommendations against the language, compiler, runtime, framework, workload, and deployment environment actually in use. Prefer newer platform capabilities only when they demonstrably improve performance, memory use, correctness, or readability.
- Support system-level recommendations with relevant code, configuration, profiling, tracing, metrics, benchmarks, or production measurements. State the expected benefit and a practical way to validate it.

# 2) Work, Complexity & Data Access

- Repeated parsing, serialization, reflection, model creation, dependency discovery, formatting, conversion, allocation, or expensive computation in loops, requests, renders, polls, retries, callbacks, messages, or per-item workflows
- Repeated full scans, linear lookups, sorting, grouping, joining, filtering, deduplication, or representation conversion when caching, indexing, precomputation, incremental processing, a narrowed query, or a more suitable data structure would avoid the work
- Avoidable poor complexity, including nested scans, O(n^2) or worse comparisons, exponential search, or recursive, graph, dependency, backtracking, or combinatorial traversal without pruning, memoization, visited tracking, or explicit bounds
- Data structures that do not match the access pattern, such as list scans where a map, set, index, heap, trie, cache, or precomputed lookup would reduce repeated work
- Hot paths that perform unnecessary copying, sorting, filtering, formatting, string construction, allocation, or data movement
- Performance-sensitive logic whose input-size assumptions, operation limits, or expected complexity are not documented or enforced

# 3) I/O, Network & External Services

- N+1 or otherwise repeated database, file-system, network, API, subprocess, or external-tool calls
- Large synchronous reads or writes; repeated file opens, directory traversal, archive operations, process launches, network connects, or request setup
- Loading entire datasets, files, responses, reports, logs, or result sets when pagination, projection, streaming, slicing, or incremental processing would suffice
- Missing batching, deduplication, caching, indexing, throttling, pagination, streaming, or bounded retries for large or repeated external work
- Polling, refresh, or retry loops that run without useful work, have ineffective cancellation or timeout behavior, or become expensive at scale

# 4) Async, Concurrency & Coordination

- Blocking calls in asynchronous flows, sync-over-async, unnecessary task waiting, or CPU-heavy work on latency-sensitive request, UI, event-dispatch, callback, or lock-held paths
- Serial processing of independent, materially expensive CPU-bound or I/O-bound work when bounded parallelism would provide a clear benefit with the available CPU and I/O capacity
- Parallel work with no genuine parallel benefit because of serial dependencies, shared locks, a single external bottleneck, insufficient workload, or a constrained execution environment
- Unbounded fan-out, queues, retries, or degree of parallelism; oversubscription, contention amplification, or exhaustion of connection, CPU, memory, file, or service limits
- Concurrent operations without cancellation, timeout, bounded concurrency, backpressure, and deterministic cleanup or resource release
- Shared mutable state without a race-free ownership, synchronization, immutability, or message-passing strategy; overly broad critical sections, atomic hot spots, or synchronization around slow operations

# 5) Memory, Serialization & Data Volume

- Unbounded growth in collections, buffers, caches, queues, logs, transcripts, diagnostics, or accumulated state
- Large allocations, copying, encoding/decoding, format expansion, string concatenation, serialization, deserialization, or text transformation in hot loops or high-frequency paths
- Holding large intermediate results longer than needed, or materializing complete inputs and outputs when streaming, pooling, slicing, incremental processing, or bounded buffers are appropriate
- Memory use that scales with repository size, project count, rule count, task count, file count, issue count, report size, message volume, or concurrent users without an explicit bound
- Size, offset, capacity, page, chunk, buffer, duration, rate, or percentage calculations that overflow, truncate, wrap, lose precision, mix units, or turn boundary values into huge allocations, full scans, or ineffective retries
- Layout, ABI, marshaling, pinning, native-interop, or representation assumptions that introduce repeated copies, padding, conversions, or compatibility work

# 6) Runtime, OS & Deployment Topology

- Hot or low-latency paths that create excessive tasks, threads, timers, processes, system calls, context switches, allocations, locks, exceptions, reflection, dynamic dispatch, or runtime marshaling
- Language or framework conveniences that hide expensive work in tight loops, high-frequency callbacks, serialization paths, or allocation-sensitive code
- Garbage collection, reference counting, finalization, memory pressure, logging, tracing, metrics, diagnostics, formatting, or stack capture that can create latency spikes without sampling, level checks, or bounded cost
- For high-throughput, low-latency, real-time, or network-intensive paths only: inspect data-path copying, batching, backpressure, flow control, queueing, interrupt and wakeup behavior, OS/kernel resources, CPU-cache locality, NUMA, and deployment topology

# 7) Scaling & Operational Safeguards

- Behavior that is acceptable for small inputs but degrades with large repositories, many files, rules, tasks, reports, issues, messages, concurrent users, or high request volume
- Missing limits, batching, progress reporting, cancellation, timeout, or overload safeguards that make large workloads slow, opaque, or difficult to recover
- Formatting, logging, diagnostics, report generation, or observability whose cost grows with output volume without sampling, truncation, aggregation, or a clear operational bound
