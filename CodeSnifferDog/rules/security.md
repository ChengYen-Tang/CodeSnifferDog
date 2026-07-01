# 1) Input & Trust Boundaries

- Missing validation for user input, uploaded files, archives, URLs, paths, headers, environment variables, configuration values, command arguments, or model/tool output
- Untrusted data used after only partial validation, weak normalization, or validation in the wrong layer
- Input accepted in one format but consumed later with stronger assumptions about encoding, length, units, type, schema, or ownership
- Boundary confusion between user, project, repository, workspace, tenant, request, background job, plugin, native module, or external dependency
- Security-sensitive defaults that allow unsafe behavior when configuration is missing or malformed

# 2) Authorization & Access Control

- Missing ownership, permission, role, tenant, project, repository, or workspace boundary checks
- Authorization checked before data transformation but not at the final operation
- Identifier-based access that trusts caller-provided IDs, paths, names, handles, keys, or object references
- Privileged operations reachable through indirect routes, background workers, callbacks, retries, queued work, or interop boundaries
- Inconsistent access checks across read, write, delete, download, export, import, status, or diagnostic endpoints

# 3) Injection & Unsafe Execution

- Command, shell, SQL, query, template, HTML, script, path, log, format-string, expression, or dynamic-code injection
- Untrusted values concatenated into executable text, queries, selectors, templates, format strings, logs, or file paths
- Escaping, quoting, encoding, or parameterization applied for the wrong target context
- Dynamic evaluation, reflection, plugin loading, deserialization, native calls, or process execution without clear constraints
- User-controlled glob, regex, search, filter, parser, or formatting input that can cause unintended behavior or denial of service

# 4) String, Encoding & Formatting Boundaries

- Untrusted data used as a format string, template, localization key, message pattern, logger template, or UI markup
- Format placeholders, argument types, widths, encodings, or parameter counts that can mismatch at runtime
- Mixing binary data, encoded text, paths, URLs, identifiers, numbers, or pointers/references through string concatenation without clear conversion rules
- Wide/narrow, UTF-8/UTF-16, byte/character, or culture-sensitive length and comparison mixups
- Truncation, missing termination, partial decoding, invalid Unicode handling, or ambiguous normalization that can bypass validation or corrupt security decisions

# 5) File, Path & Archive Handling

- Path traversal, unsafe absolute paths, symlink/junction issues, weak canonicalization, or path comparisons before normalization
- Archive extraction that can write outside the intended directory or overwrite important files
- Temporary files or directories created with predictable names, unsafe permissions, shared locations, or missing cleanup
- File reads, writes, deletes, downloads, imports, or exports that are not scoped to the intended root
- Cross-project or cross-user file access through shared storage, cached paths, stale state, or reused handles

# 6) Memory, Buffer & Native Boundary Exposure

- Unsafe buffer, span, slice, pointer, handle, or native-memory use reachable from untrusted input
- Out-of-bounds reads/writes, off-by-one access, negative index handling, or size calculation errors that can expose or corrupt data
- Manual memory, unsafe code, FFI/native interop, serialization, or binary parsing that trusts caller-controlled lengths or layouts
- Cross-module allocation/free, ownership transfer, or handle lifetime confusion at language/runtime boundaries
- Unsafe casts, reinterpretation, alignment assumptions, or layout assumptions that can expose memory or bypass validation

# 7) Secret & Sensitive Data Exposure

- Secrets, credentials, API keys, tokens, connection strings, private URLs, or sensitive payloads written to logs, errors, reports, UI, telemetry, dumps, or persisted state
- Sensitive values returned to clients or stored in plain text without a clear need
- Exception messages that expose internal paths, query details, stack traces, credentials, object state, or private data
- Debug or diagnostic logging that can expose data from another project, user, request, or trust boundary
- Missing redaction at trust boundaries where data leaves the process
