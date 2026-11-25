# Time, Clocks & Timer Resolution

`ZeroAlloc.Platform` provides three complementary timing primitives that separate hot-path timestamping, deterministic testing, and OS scheduler sleep precision.

---

## 1. `NativeTsc` (Hardware Monotonic UTC Clock)

`NativeTsc.NowNs()` and `NativeTsc.NowMs()` return UTC Unix epoch timestamps without allocating or calling into the kernel on x64:

- On 64-bit x64 (Linux and Windows), the static constructor maps a 10-byte `RDTSC` instruction stub, calibrates the TSC frequency over a 10 ms window against `Stopwatch`, and anchors it to `DateTime.UtcNow`.
- A lightweight background timer recalibrates the anchor every 10 seconds under a 64-byte Seqlock. Recalibration clamps against the previous projected timestamp so wall-clock NTP adjustments never cause `NowNs()` to step backwards.
- Delta scaling uses a 128-bit multiply-shift (`Math.BigMul` on .NET / partial products on `netstandard2.1`) and guards `tsc > baseTsc` so unsigned underflow or 64-bit overflow cannot occur.
- On ARM64 (Linux ARM64, Apple Silicon) or W^X-restricted environments where executable pages are denied, `NativeTsc` automatically falls back to a UTC-anchored `Stopwatch.GetTimestamp()` implementation. You can inspect which path is active via `NativeTsc.IsHardwareTscActive`.

```csharp
using Platform.Time;

long nowNs = NativeTsc.NowNs();
long nowMs = NativeTsc.NowMs();
```

---

## 2. `PlatformClock` & `PlatformClockBlock` (Deterministic Simulation Time)

In simulation engines, network timeouts, and unit tests, calling `Thread.Sleep` to test TTL expiration or heartbeat timeouts is slow and flaky. `PlatformClockBlock` is a 64-byte unmanaged struct that can live on the stack, in unmanaged memory, or inside a shared-memory control block:

```csharp
using Platform.Time;

PlatformClockBlock clock = default;

// Live mode (IsManual == 0): delegates directly to NativeTsc
long live = PlatformClock.NowNs(&clock);

// Deterministic manual mode: freezes time and advances in discrete steps
PlatformClock.SetManualTime(&clock, 1_700_000_000_000_000_000L);
PlatformClock.AdvanceTime(&clock, TimeSpan.FromMilliseconds(250));
long simulated = PlatformClock.NowNs(&clock); // 1_700_000_000_250_000_000L

// Return to live hardware clock
PlatformClock.ResetToLive(&clock);
```

Passing `null` (or a near-null struct-member offset `< 4096`) to `PlatformClock.NowNs(clock)` or `PlatformClock.NowMs(clock)` safely falls back to live `NativeTsc` without faulting.

---

## 3. `HighResolutionTimerScope` (Windows Scheduler Precision)

On Windows, the default OS scheduler timer tick is ~15.6 ms (64 Hz). Without raising it, calls like `Thread.Sleep(1)`, `ThreadParker.Park(1)`, or `CrossProcessThreadParker.Park(1)` can oversleep by up to 15 ms.

`HighResolutionTimerScope` wraps Win32 `timeBeginPeriod` / `timeEndPeriod` in an idempotent RAII scope:

```csharp
using Platform.Timer;

using var timerScope = new HighResolutionTimerScope(periodMs: 1);
// On Windows: scheduler resolution is now 1 ms (timerScope.IsActive == true)
// On Linux/macOS: safe no-op (timerScope.IsActive == false)
```

Keep one scope alive for the lifetime of a real-time loop or server process rather than creating and disposing it per frame. Calling `Dispose()` more than once is safe and will not double-decrement the Windows timer refcount.
