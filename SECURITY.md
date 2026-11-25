# Security Policy

## Reporting a Vulnerability

ZeroAlloc.Platform uses `unsafe` code, raw pointers, native P/Invoke bindings, executable JIT memory (`RDTSC` stub), and cross-process OS shared memory (`shm_open`, `mmap`, `CreateFileMappingW`). Memory-safety bugs, native handle leaks, permission issues on shared-memory segments, or out-of-bounds access bugs should not be reported in public GitHub issues.

Please report potential security or memory-safety vulnerabilities privately via GitHub Private Vulnerability Reporting:

- Go to the repository's Security tab -> Advisories -> Report a vulnerability.

When reporting, please include:
- Affected version (`ZeroAlloc.Platform` version / commit)
- OS and CPU architecture (e.g., Windows x64, Linux ARM64, macOS ARM64)
- Target framework (`net10.0` or `netstandard2.1`)
- A minimal reproduction or description of the memory-safety or security impact

You will receive an acknowledgment as soon as possible, and a fix will be coordinated before public disclosure.
