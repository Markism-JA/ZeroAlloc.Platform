# ZeroAlloc.Platform

Zero-allocation operating system primitives for **.NET 10** and **netstandard2.1** (64-bit AnyCPU: `x64` & `ARM64`).

Built as Layer 0b of the `ZeroAlloc` ecosystem on top of [`ZeroAlloc.Memory`](https://www.nuget.org/packages/ZeroAlloc.Memory), providing direct OS virtual/shared memory maps, RDTSC Seqlock clocks, atomic two-phase thread parkers, CPU core affinity pinning, and out-of-band crash diagnostics.

---

## Packages

| Package | Targets | Description |
| :--- | :--- | :--- |
| **`ZeroAlloc.Platform`** | `net10.0;netstandard2.1` | Core OS primitives: `NativeTsc`, `PlatformClock`, `ThreadParker`, `CrossProcessThreadParker`, `ThreadAffinityUtility`, `NativeMemoryMap`, `SharedMemorySession`, `SharedMemoryScanner`, `NativeDiagnostics`, `HighResolutionTimerScope`. |
| **`ZeroAlloc.Platform.Benchmarking`** | `net10.0` | Minimal low-level macro-probe benchmarking harness (`PinnedWorkerThreads`, `OpenLoopPacer`, `SeqlockMetricsReader`, `UnmanagedLinearHistogram`). |

---

## Platform Support Matrix

`ZeroAlloc.Platform` uses `#if NET7_0_OR_GREATER` (`[LibraryImport]` source-generated P/Invoke) on `.NET 10` and `[DllImport]` on `netstandard2.1`.

| Subsystem | Linux (`x64` / `arm64`) | Windows (`x64` / `arm64`) | macOS (`arm64` / `x64`) | Notes |
| :--- | :---: | :---: | :---: | :--- |
| **`NativeTsc` & `PlatformClock`** | Full | Full | Full | Hardware `RDTSC` + lock-free Seqlock on `x64`; UTC-anchored `Stopwatch` (`CNTVCT_EL0` / `mach_absolute_time`) on `ARM64` & W^X-restricted runtimes. |
| **`ThreadParker`** | Native `sem_t` | Native Win32 Event | Managed `AutoResetEvent` | Atomic two-phase `TryArm` / `Disarm` / `Park` / `Unpark` with syscall elision while running. |
| **`NativeMemoryMap`** | Full (`mmap` / `shm_open`) | Full (`CreateFileMappingW`) | Full (`mmap` + `/tmp/zeroalloc_shm`) | Anonymous, named IPC shared memory, and persistent file-backed mappings. |
| **`SharedMemorySession` & `Plan`** | Full | Full | Full (`16 KB` page-aware) | Page 0 superblock + aligned typed regions (`AsSpan<T>`, `AsRef<T>`, `Read<T>`, `Write<T>`). |
| **`SharedMemoryScanner`** | Full (`/dev/shm`) | Full (`\BaseNamedObjects`) | Full (`/tmp/zeroalloc_shm`) | Zero-allocation discovery of active `Ready` superblocks. |
| **`NativeDiagnostics`** | Native `write(2)` | Native `WriteFile` | Native `write(2)` | Zero-allocation direct `stderr` sink bypassing managed `Console` locks. |
| **`HighResolutionTimerScope`** | No-op | `timeBeginPeriod` | No-op | Idempotent RAII scope for Windows timer resolution. |
| **`CrossProcessThreadParker`** | Full (`sem_open` + `sem_timedwait`) | Full (Named Event) | `PlatformNotSupportedException` | Darwin libc omits `sem_timedwait` (matches .NET BCL named `EventWaitHandle`). |
| **`ThreadAffinityUtility`** | Full (`pthread_setaffinity_np`) | Full (`SetThreadAffinityMask`) | `PlatformNotSupportedException` | Apple Silicon / XNU kernel forbids hard CPU core pinning. |

---

## Benchmarks (.NET 10.0.2, Linux x64)

### Time & Clocks (`ClockBenchmarks`)

| Method | Mean | Ratio vs `Stopwatch` | Allocated |
| :--- | ---: | ---: | ---: |
| `Stopwatch_GetTimestamp` *(baseline)* | `16.27 ns` | `1.00x` | `0 B` |
| `DateTime_UtcNow_Ticks` | `19.56 ns` | `1.20x` | `0 B` |
| **`NativeTsc_NowNs`** | **`9.02 ns`** | **`0.55x`** | **`0 B`** |
| **`PlatformClock_NowNs_Live`** | **`9.03 ns`** | **`0.56x`** | **`0 B`** |
| **`PlatformClock_NowNs_Manual`** | **`0.57 ns`** | **`0.03x`** | **`0 B`** |

### Thread Parking (`ThreadParkerBenchmarks`)

| Method | Mean | Ratio vs `AutoResetEvent` | Allocated |
| :--- | ---: | ---: | ---: |
| `Bcl_AutoResetEvent_Set` *(baseline)* | `173.60 ns` | `1.00x` | `0 B` |
| **`ThreadParker_Unpark_ElidedWhileRunning`** | **`9.23 ns`** | **`0.05x`** | **`0 B`** |
| **`ThreadParker_TryArm_Disarm`** | **`14.37 ns`** | **`0.08x`** | **`0 B`** |

### Memory Maps & Shared Memory (`MemoryMapBenchmarks` & `SharedMemoryBenchmarks`)

| Method | Mean | Allocated |
| :--- | ---: | ---: |
| `TypedSpan_Sum64KB` (`64 KB` mapped view) | `3.24 µs` | `0 B` |
| `Session_GetRegion` | `4.05 ns` | `0 B` |
| `CachedRegion_ReadInt64` | `0.33 ns` | `0 B` |
| `CachedRegion_AsRefInt64` | `0.62 ns` | `0 B` |

---

## Building & Testing

```bash
dotnet build ZeroAlloc.Platform.slnx -c Release
dotnet test ZeroAlloc.Platform.slnx -c Release
dotnet test ZeroAlloc.Platform.slnx -c Debug
```

---

## License

MIT License — see [LICENSE](LICENSE).
