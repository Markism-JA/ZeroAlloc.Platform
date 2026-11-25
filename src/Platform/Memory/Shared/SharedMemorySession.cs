using Platform.Internal;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Platform.Memory.Shared;

public sealed unsafe class SharedMemorySession : IDisposable
{
    private readonly NativeMemoryMap _map;
    private readonly SharedMemorySuperblock* _superblock;
    private readonly SharedMemoryRegion[] _cachedRegions;
    private readonly bool _isOwner;
    private int _isDisposed;

    public ushort NodeId => _superblock->NodeId;
    public nuint TotalLength => _superblock->TotalByteLength;
    public bool IsOwner => _isOwner;
    public SharedMemoryState State => (SharedMemoryState)Volatile.Read(ref _superblock->State);
    public long CreatedTimestampTicks => _superblock->CreatedTimestampTicks;
    public NativeMemoryMap UnderlyingMap => _map;

    private SharedMemorySession(NativeMemoryMap map, bool isOwner)
    {
        _map = map;
        _superblock = (SharedMemorySuperblock*)map.Pointer;
        _isOwner = isOwner;

        // Cache all region descriptors into direct pointer projections upfront
        int count = _superblock->RegionCount;
        _cachedRegions = new SharedMemoryRegion[count];
        SharedMemoryRegionEntry* entries = _superblock->GetRegionEntries();

        for (int i = 0; i < count; i++)
        {
            _cachedRegions[i] = new SharedMemoryRegion(
                map.Pointer + entries[i].Offset,
                entries[i].Length,
                entries[i].Tag,
                entries[i].Offset
            );
        }
    }

    #region Unified Factories (IPC Shared Memory)

    /// <summary>
    /// Computes layout sizing, allocates the shared memory segment, and publishes it in the Ready state.
    /// </summary>
    public static SharedMemorySession CreateShared(
        string name,
        uint magic,
        uint abiVersion,
        ushort nodeId,
        SharedMemoryPlan plan,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.PreFault
    )
    {
        PlatformCompat.ThrowIfNull(plan);
        nuint totalBytes = plan.CalculateTotalBytes();
        var map = NativeMemoryMap.CreateNewShared(name, totalBytes, protection, options);
        return Create(map, magic, abiVersion, nodeId, plan);
    }

    /// <summary>
    /// Attaches to an existing shared memory segment and validates ABI and partition integrity.
    /// </summary>
    public static SharedMemorySession AttachShared(
        string name,
        uint expectedMagic,
        uint expectedAbiVersion,
        SharedMemoryPlan plan,
        TimeSpan timeout = default,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNull(plan);
        nuint totalBytes = plan.CalculateTotalBytes();
        var map = NativeMemoryMap.OpenExistingShared(name, totalBytes, protection, options);
        return Attach(map, expectedMagic, expectedAbiVersion, timeout);
    }

    /// <summary>
    /// Creates the shared memory segment if it does not exist, or attaches to it if already running.
    /// </summary>
    public static SharedMemorySession OpenOrCreateShared(
        string name,
        uint magic,
        uint abiVersion,
        ushort nodeId,
        SharedMemoryPlan plan,
        TimeSpan timeout = default,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.PreFault
    )
    {
        PlatformCompat.ThrowIfNull(plan);
        nuint totalBytes = plan.CalculateTotalBytes();
        var map = NativeMemoryMap.OpenOrCreateShared(
            name,
            totalBytes,
            protection,
            options,
            timeout
        );

        var sb = (SharedMemorySuperblock*)map.Pointer;
        uint state = Volatile.Read(ref sb->State);

        if (state == (uint)SharedMemoryState.Uninitialized)
        {
            return Create(map, magic, abiVersion, nodeId, plan);
        }

        return Attach(map, magic, abiVersion, timeout);
    }

    #endregion

    #region Unified Factories (Persistent Disk Files)

    public static SharedMemorySession CreateFile(
        string filePath,
        uint magic,
        uint abiVersion,
        ushort nodeId,
        SharedMemoryPlan plan,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNull(plan);
        nuint totalBytes = plan.CalculateTotalBytes();
        var map = NativeMemoryMap.CreateNewFile(filePath, totalBytes, protection, options);
        return Create(map, magic, abiVersion, nodeId, plan);
    }

    public static SharedMemorySession OpenFile(
        string filePath,
        uint expectedMagic,
        uint expectedAbiVersion,
        SharedMemoryPlan plan,
        TimeSpan timeout = default,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNull(plan);
        nuint totalBytes = plan.CalculateTotalBytes();
        var map = NativeMemoryMap.OpenExistingFile(filePath, totalBytes, protection, options);
        return Attach(map, expectedMagic, expectedAbiVersion, timeout);
    }

    #endregion

    #region Low-Level Lifecycle Formatting

    public static SharedMemorySession Create(
        NativeMemoryMap map,
        uint magic,
        uint abiVersion,
        ushort nodeId,
        SharedMemoryPlan plan
    )
    {
        PlatformCompat.ThrowIfNull(map);
        PlatformCompat.ThrowIfNull(plan);

        var sb = (SharedMemorySuperblock*)map.Pointer;
        plan.FormatSuperblock(sb, magic, abiVersion, nodeId, map.Length);

        Thread.MemoryBarrier();
        Volatile.Write(ref sb->State, (uint)SharedMemoryState.Ready);

        return new SharedMemorySession(map, isOwner: true);
    }

    public static SharedMemorySession Attach(
        NativeMemoryMap map,
        uint expectedMagic,
        uint expectedAbiVersion,
        TimeSpan timeout = default
    )
    {
        PlatformCompat.ThrowIfNull(map);

        if (map.Length < SharedMemorySuperblock.SuperblockSize)
        {
            throw new InvalidDataException(
                "Memory segment is too small to contain the superblock."
            );
        }

        var sb = (SharedMemorySuperblock*)map.Pointer;

        long start = Stopwatch.GetTimestamp();
        long timeoutTicks =
            timeout == default
                ? Stopwatch.Frequency * 5
                : (long)(timeout.TotalSeconds * Stopwatch.Frequency);

        SpinWait spinner = default;
        while (true)
        {
            uint state = Volatile.Read(ref sb->State);
            if (state == (uint)SharedMemoryState.Ready)
                break;

            if (state == (uint)SharedMemoryState.Faulted)
                throw new InvalidOperationException(
                    "Shared memory segment is marked Faulted by host."
                );

            if (state == (uint)SharedMemoryState.TornDown)
                throw new InvalidOperationException("Shared memory segment has been torn down.");

            if (Stopwatch.GetTimestamp() - start > timeoutTicks)
                throw new TimeoutException(
                    "Timed out waiting for shared memory segment to become Ready."
                );

            spinner.SpinOnce();
        }

        Thread.MemoryBarrier();

        if (sb->Magic != expectedMagic)
            throw new InvalidDataException(
                $"Magic mismatch. Expected 0x{expectedMagic:X8}, found 0x{sb->Magic:X8}."
            );

        if (sb->AbiVersion != expectedAbiVersion)
            throw new InvalidDataException(
                $"ABI Version mismatch. Expected v{expectedAbiVersion}, found v{sb->AbiVersion}."
            );

        if (map.Length < sb->TotalByteLength)
            throw new InvalidDataException(
                $"Backing mapping ({map.Length}B) is smaller than segment ({sb->TotalByteLength}B)."
            );

        ValidatePartitionInvariants(sb);

        return new SharedMemorySession(map, isOwner: false);
    }

    #endregion

    #region Fast O(1) Region Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedMemoryRegion GetRegion(uint tag)
    {
        PlatformCompat.ThrowIfDisposed(_isDisposed != 0, this);

        for (int i = 0; i < _cachedRegions.Length; i++)
        {
            if (_cachedRegions[i].Tag == tag)
                return _cachedRegions[i];
        }

        throw new KeyNotFoundException($"Partition region tag '0x{tag:X8}' not found.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedMemoryRegion GetRegion<TEnum>(TEnum tag)
        where TEnum : struct, Enum
    {
        uint rawTag = Unsafe.SizeOf<TEnum>() switch
        {
            4 => PlatformCompat.BitCast<TEnum, uint>(tag),
            2 => PlatformCompat.BitCast<TEnum, ushort>(tag),
            1 => PlatformCompat.BitCast<TEnum, byte>(tag),
            _ => (uint)Convert.ToInt64(tag),
        };

        return GetRegion(rawTag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetRegionSpan(uint tag) => GetRegion(tag).AsSpan();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetRegionSpan<T>(uint tag)
        where T : unmanaged => GetRegion(tag).AsSpan<T>();

    #endregion

    #region Health, Heartbeat & Diagnostics

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeatHeartbeat()
    {
        PlatformCompat.ThrowIfDisposed(_isDisposed != 0, this);
        Volatile.Write(ref _superblock->HeartbeatTicks, Stopwatch.GetTimestamp());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetHeartbeatAgeTicks()
    {
        long last = Volatile.Read(ref _superblock->HeartbeatTicks);
        return last == 0 ? long.MaxValue : Stopwatch.GetTimestamp() - last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan GetHeartbeatAge()
    {
        long ticks = GetHeartbeatAgeTicks();
        return ticks == long.MaxValue
            ? TimeSpan.MaxValue
            : TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPeerAlive(TimeSpan timeout) => GetHeartbeatAge() <= timeout;

    /// <summary>
    /// Signals to all peer processes that this shared region encountered critical corruption.
    /// </summary>
    public void MarkFaulted()
    {
        PlatformCompat.ThrowIfDisposed(_isDisposed != 0, this);
        Volatile.Write(ref _superblock->State, (uint)SharedMemoryState.Faulted);
        Thread.MemoryBarrier();
    }

    #endregion

    private static void ValidatePartitionInvariants(SharedMemorySuperblock* sb)
    {
        int count = sb->RegionCount;
        if (count > SharedMemorySuperblock.MaxRegions)
            throw new InvalidDataException($"Corrupted region count: {count}.");

        SharedMemoryRegionEntry* entries = sb->GetRegionEntries();
        nuint lastEnd = SharedMemorySuperblock.SuperblockSize;

        for (int i = 0; i < count; i++)
        {
            ref readonly var entry = ref entries[i];

            if (entry.Offset < lastEnd || entry.Offset + entry.Length > sb->TotalByteLength)
            {
                throw new InvalidDataException(
                    $"Region tag 0x{entry.Tag:X8} [Offset: {entry.Offset}, Len: {entry.Length}] breaches boundaries."
                );
            }

            if ((entry.Offset % entry.Alignment) != 0)
            {
                throw new InvalidDataException(
                    $"Region tag 0x{entry.Tag:X8} offset {entry.Offset} violates alignment requirement of {entry.Alignment}."
                );
            }

            lastEnd = entry.Offset + entry.Length;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        if (_isOwner && _superblock != null)
        {
            Volatile.Write(ref _superblock->State, (uint)SharedMemoryState.TornDown);
            Thread.MemoryBarrier();
        }

        _map.Dispose();
    }
}
