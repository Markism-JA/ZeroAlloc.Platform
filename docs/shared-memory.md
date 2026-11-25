# Partitioned Shared Memory & Discovery

`Platform.Memory.Shared` and `Platform.Memory.Scanner` provide a structured, cross-process memory layout over `NativeMemoryMap` so independent processes can share typed data structures without serialization, sockets, or heap allocations.

---

## 1. Memory Layout

Every segment formatted by `SharedMemoryPlan` reserves Page 0 for a `SharedMemorySuperblock` and places user partitions starting at Page 1 (`max(4096, Environment.SystemPageSize)` — 4 KB on x64 Linux/Windows, 16 KB on Apple Silicon):

```text
Offset 0                  ┌────────────────────────────────────────────────────┐
                          │ Cache Line 0 (64 B): Magic, AbiVersion, State,     │
                          │   NodeId, RegionCount, TotalByteLength,            │
                          │   CreatedTimestampTicks, HeartbeatTicks            │
Offset 64                 ├────────────────────────────────────────────────────┤
                          │ Partition Descriptor Table (up to 126 × 32 B)      │
                          │   [Tag, Alignment, Offset, Length, Reserved]       │
                          ├────────────────────────────────────────────────────┤
Offset = PageBoundary     │ Region 0 (aligned to requested power-of-two)       │
                          ├────────────────────────────────────────────────────┤
                          │ Region 1 (aligned to requested power-of-two)       │
                          └────────────────────────────────────────────────────┘
```

Isolating Page 0 from user partitions keeps control-plane heartbeats and lifecycle state away from hot data-plane writes.

---

## 2. Owner / Client Lifecycle

1. Define a deterministic layout with `SharedMemoryPlan` using `FourCC` or enum tags:

```csharp
using Platform.Memory.Shared;

enum TelemetryRegion : uint
{
    Control = 0x4354524C, // "CTRL"
    Ring    = 0x52494E47, // "RING"
}

var plan = new SharedMemoryPlan()
    .AddRegion(TelemetryRegion.Control, byteCount: 64, alignment: 64)
    .AddRegion(TelemetryRegion.Ring, byteCount: 64 * 1024, alignment: 64);
```

2. The owner process creates and publishes the segment. `CreateShared` calculates the exact byte size, allocates the OS shared memory segment, formats Page 0, issues a memory barrier, and publishes `SharedMemoryState.Ready`:

```csharp
uint magic = FourCC.Make("ECHO");

using var owner = SharedMemorySession.CreateShared(
    name: "echoes_node_01",
    magic: magic,
    abiVersion: 1,
    nodeId: 1,
    plan: plan
);

owner.BeatHeartbeat();
```

3. Peer processes attach with `AttachShared`. Attachment spins until `State == Ready` (failing immediately on `Faulted` or `TornDown`), then validates `Magic`, `AbiVersion`, total length, and non-overlapping partition alignment before returning:

```csharp
using var peer = SharedMemorySession.AttachShared(
    name: "echoes_node_01",
    expectedMagic: magic,
    expectedAbiVersion: 1,
    plan: plan,
    timeout: TimeSpan.FromSeconds(5)
);
```

4. When the owner disposes, it writes `SharedMemoryState.TornDown`, unmaps the view, and unlinks the shared name. Non-owner peers unmap without unlinking.

For persistent disk-backed snapshots or flight recorders, `SharedMemorySession.CreateFile` and `SharedMemorySession.OpenFile` follow the exact same Page 0 format over a file on disk.

---

## 3. Zero-Copy Region Projections

Resolve regions once during startup and cache the `SharedMemoryRegion` value type on hot loops:

```csharp
SharedMemoryRegion ringRegion = owner.GetRegion(TelemetryRegion.Ring);

// Project a fixed control header followed by a contiguous array of items
ref RingHeader header = ref ringRegion.GetHeaderAndTrailingSpan<RingHeader, long>(out Span<long> slots);
slots[0] = 42;

// Or direct bounds-checked Read/Write/AsRef
ringRegion.Write(0, 1234L);
long val = ringRegion.Read<long>(0);
```

---

## 4. Discovering Live Segments (`SharedMemoryScanner`)

`SharedMemoryScanner.Scan` enumerates active shared segments matching a name prefix into a caller-provided `Span<DiscoveredSegment>` with zero heap allocations:

```csharp
using Platform.Memory.Scanner;

Span<DiscoveredSegment> found = stackalloc DiscoveredSegment[16];
int count = SharedMemoryScanner.Scan(
    prefix: "echoes_node_",
    destination: found,
    expectedMagic: FourCC.Make("ECHO"),
    expectedAbiVersion: 1
);

for (int i = 0; i < count; i++)
{
    bool alive = found[i].IsHeartbeatAlive(TimeSpan.FromSeconds(5));
}
```

### OS Backing Details
- Linux: `shm_open` / `shm_unlink` in `/dev/shm`, scanned via `opendir` + `readdir`.
- Windows: `CreateFileMappingW` / `OpenFileMappingW` in `Local\`, scanned via `NtOpenDirectoryObject` + `NtQueryDirectoryObject` on `\Sessions\<id>\BaseNamedObjects`.
- macOS: File-backed `mmap(MAP_SHARED)` in `/tmp/zeroalloc_shm/` (avoids Darwin's 31-character `shm_open` limit and enables directory enumeration), scanned via `/tmp/zeroalloc_shm/`.
