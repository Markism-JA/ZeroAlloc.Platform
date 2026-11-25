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

## Benchmarks

Measured with **BenchmarkDotNet v0.15.8** (`DefaultJob`, `[MemoryDiagnoser]`) on **.NET 10.0.2 (X64 RyuJIT x86-64-v4)**, Linux Arch Linux (11th Gen Intel Core i3-1115G4).

### Time & Clocks (`ClockBenchmark`)

| Method | Mean | Error | Ratio | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| `Stopwatch_GetTimestamp` *(baseline)* | `18.99 ns` | `±0.26 ns` | `1.00x` | `0 B` |
| `DateTime_UtcNow_Ticks` | `21.66 ns` | `±0.27 ns` | `1.14x` | `0 B` |
| **`NativeTsc_NowNs`** | **`8.93 ns`** | **`±0.09 ns`** | **`0.47x` (2.1x faster)** | **`0 B`** |
| **`PlatformClock_NowNs_Live`** | **`11.10 ns`** | **`±0.22 ns`** | **`0.58x` (1.7x faster)** | **`0 B`** |
| **`PlatformClock_NowNs_Manual`** | **`0.87 ns`** | **`±0.02 ns`** | **`0.05x`** | **`0 B`** |

### Thread Parking & Wake (`ThreadParkerBenchmark`)

| Method | Mean | Error | Ratio | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| `Bcl_AutoResetEvent_Set_Uncontended` *(baseline)* | `220.87 ns` | `±5.50 ns` | `1.00x` | `0 B` |
| **`ThreadParker_Unpark_ElidedWhileRunning`** | **`5.63 ns`** | **`±0.11 ns`** | **`0.03x` (39x faster)** | **`0 B`** |
| **`ThreadParker_TryArm_Disarm`** | **`15.96 ns`** | **`±0.11 ns`** | **`0.07x` (14x faster)** | **`0 B`** |
| `Bcl_AutoResetEvent_PingPongRoundTrip` *(2-thread wake)* | `6,920.17 ns` | `±137.73 ns` | `1.00x` | `0 B` |
| **`ThreadParker_PingPongRoundTrip`** *(2-thread wake)* | **`4,835.46 ns`** | **`±95.66 ns`** | **`0.70x` (1.43x faster)** | **`0 B`** |

### Memory Maps & Shared Memory (`NativeMemoryMapBenchmark` & `SharedMemoryBenchmark`)

| Method | Mean | Error | Allocated |
| :--- | ---: | ---: | ---: |
| `ManagedArray_SequentialSum_64KB` *(baseline)* | `4.01 µs` | `±0.12 µs` | `0 B` |
| **`NativeMemoryMap_TypedSpan_SequentialSum_64KB`** | **`4.08 µs`** | **`±0.11 µs`** | **`0 B`** |
| `AnonymousMap_DemandPaged_CreateAndTouchAllPages_64KB` | `18.21 µs` | `±0.66 µs` | `72 B` |
| **`AnonymousMap_PreFault_CreateAndTouchAllPages_64KB`** | **`16.58 µs`** | **`±0.33 µs`** | **`72 B`** |
| **`Session_GetRegion`** | **`2.06 ns`** | **`±0.06 ns`** | **`0 B`** |
| **`CachedRegion_ReadInt64`** | **`0.87 ns`** | **`±0.03 ns`** | **`0 B`** |
| **`CachedRegion_AsRefInt64`** | **`0.98 ns`** | **`±0.05 ns`** | **`0 B`** |

---

## Building, Testing & Benchmarking

```bash
dotnet build ZeroAlloc.Platform.slnx -c Release
dotnet test ZeroAlloc.Platform.slnx -c Release
dotnet test ZeroAlloc.Platform.slnx -c Debug

# Run benchmarks
dotnet run --project benchmark/Platform.Benchmark -c Release -- --filter "*"
```

---

## License

MIT License — see [LICENSE](LICENSE).
