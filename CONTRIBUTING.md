# Contributing to ZeroAlloc.Platform

Thank you for your interest in contributing to ZeroAlloc.Platform. This is a focused, solo-maintained, low-level OS primitives library—keeping changes small, well-tested, and tightly scoped helps keep it reliable across platforms.

## Before You Start

- Open an issue first before starting work on new OS primitives, shared-memory layout changes, or public API additions so we can align on scope and platform support.
- Bug fixes, documentation improvements, and tests are always welcome via pull request.
- AI-assisted contributions are fine, provided you have read the relevant module, understand its safety invariants, and take full responsibility for every line in the PR.
- Maintenance happens on a best-effort schedule; PRs outside the library's core scope may be declined.

## Development Requirements

- .NET SDK matching `global.json` (targets `net10.0` and `netstandard2.1`).
- Zero warnings: builds must succeed with `0 Warning(s)` and `0 Error(s)` in `Release` mode, including `CS1591` XML documentation on all public types and members.
- Target frameworks: changes must compile cleanly on both `net10.0` (`[LibraryImport]`) and `netstandard2.1` (`[DllImport]`), built as 64-bit AnyCPU (`x64` and `ARM64`).

### Local Verification

```bash
dotnet build ZeroAlloc.Platform.slnx --no-incremental -c Release
dotnet test ZeroAlloc.Platform.slnx -c Release --no-build
dotnet test ZeroAlloc.Platform.slnx -c Debug
```

## Expectations for Native Interop & `unsafe` Code

Because ZeroAlloc.Platform calls directly into OS kernels (`mmap`, `shm_open`, `sem_t`, `CreateFileMappingW`, `pthread_setaffinity_np`, `RDTSC` JIT stubs):

1. Tests are mandatory: PRs touching P/Invoke bindings, shared-memory layouts, clocks, or thread parking must include unit tests (and concurrency tests where applicable).
2. Document platform parity explicitly: if an OS lacks a physical equivalent for a capability, throw `PlatformNotSupportedException` cleanly rather than silently misbehaving, and update the support matrix in `README.md`.
3. No unverified performance claims: PRs claiming latency or allocation improvements should include BenchmarkDotNet results from `benchmark/Platform.Benchmark`.
4. Preserve safety invariants: document ownership, handle lifetimes, page-alignment rules, and memory-ordering assumptions in XML docs and inline comments.
