# 1) Structure & Responsibility

- Mixed responsibilities that make one component handle unrelated concerns
- Low cohesion: one module mixes unrelated reasons to change instead of grouping behavior around a clear responsibility
- Hidden coupling between modules, workflows, tools, UI, storage, configuration, external services, generated code, native modules, or platform-specific code
- High coupling: callers depend on internal state, concrete implementations, ordering, side effects, storage layout, framework details, or unrelated modules
- Changes that require coordinated edits across many files or layers because responsibilities are not well isolated
- Control flow spread across files in a way that makes behavior hard to trace
- Important behavior encoded through naming, ordering, conventions, build settings, file layout, or side effects rather than explicit contracts
- Abstractions that leak implementation details or force callers to know internal state, lifetime, threading, or ownership rules

# 2) Complexity & Readability

- Overly complex branching, nesting, state machines, retry loops, interop code, parsing logic, or orchestration logic
- Duplicated logic that can diverge across agents, workflows, services, UI components, tests, language bindings, or platform variants
- Magic strings, numbers, paths, keys, statuses, timing values, format strings, schema fields, or layout constants that define behavior but are scattered
- Naming that obscures intent, ownership, lifecycle, units, encoding, state transitions, or domain meaning
- Code that is difficult to review because important assumptions are implicit

# 3) API, Contract & Boundary Clarity

- Public or internal APIs with unclear preconditions, postconditions, null/empty behavior, error behavior, lifetime rules, ownership transfer, threading rules, or disposal requirements
- Methods that accept broad or loosely typed data where a narrower domain model would prevent misuse
- Inconsistent naming, return values, exceptions, status values, validation, units, encoding, or formatting across similar operations
- Callers required to perform steps in a fragile order without compiler, type-system, runtime, or test enforcement
- Contract changes that can break existing callers without obvious test coverage

# 4) Interop, Layout & Compatibility

- Producer and consumer disagree on schema, field order, struct layout, packing, serialization shape, calling convention, encoding, time zone, units, or versioning
- Native/managed, service/client, plugin/host, process/process, or module/module boundaries lack clear ownership and compatibility rules
- Casts, adapters, mappers, DTOs, generated types, or wrappers hide important compatibility assumptions
- Platform-specific behavior is scattered instead of isolated behind explicit abstractions
- Build, publish, packaging, or copy-to-output behavior is required for runtime correctness but is not documented or tested

# 5) Testability & Regression Risk

- Important behavior that cannot be tested without heavy integration setup, real external services, native dependencies, specific OS state, or real timing
- Missing tests around workflow transitions, retry, cancellation, failure paths, boundaries, parsing, size calculations, resource lifecycle, or data mapping
- Tests that assert incidental markup, wording, ordering, timing, package versions, or implementation details too tightly
- Test seams that make production code harder to understand or allow unrealistic behavior
- Changes likely to create regressions because related behavior is not covered or not localized

# 6) Operability & Debuggability

- Missing contextual logs or diagnostics around important operational boundaries
- Error messages that do not identify the project, rule, task, file, operation, boundary, resource, or recovery action
- State transitions that are hard to inspect from logs, UI, persisted records, metrics, traces, dumps, or reports
- Configuration behavior that is difficult to discover, override, validate, or reason about
- Failures that require source-level debugging because runtime evidence is insufficient
