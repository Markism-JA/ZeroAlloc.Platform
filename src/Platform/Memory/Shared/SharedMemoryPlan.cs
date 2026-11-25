using System.Runtime.CompilerServices;
using Platform.Internal;

namespace Platform.Memory.Shared;

/// <summary>
/// Declarative layout builder that computes page-aligned partition offsets and formats Page 0 of a <see cref="SharedMemorySuperblock"/>.
/// </summary>
public sealed class SharedMemoryPlan
{
    private readonly struct PlannedRegion(uint tag, nuint length, nuint alignment)
    {
        public readonly uint Tag = tag;
        public readonly nuint Length = length;
        public readonly nuint Alignment = alignment;
    }

    private readonly List<PlannedRegion> _regions = new(SharedMemorySuperblock.MaxRegions);

    private static nuint PageBoundary =>
        PlatformCompat.SystemPageSize > SharedMemorySuperblock.SuperblockSize
            ? PlatformCompat.SystemPageSize
            : SharedMemorySuperblock.SuperblockSize;

    /// <summary>Adds a sub-partition identified by a raw 32-bit tag.</summary>
    public SharedMemoryPlan AddRegion(uint tag, nuint byteCount, nuint alignment = 64)
    {
        if (_regions.Count >= SharedMemorySuperblock.MaxRegions)
            throw new InvalidOperationException(
                $"Cannot exceed {SharedMemorySuperblock.MaxRegions} partitions in Page 0 descriptor table."
            );

        PlatformCompat.ThrowIfZero(byteCount, nameof(byteCount));
        if (alignment < 8 || !PlatformCompat.IsPow2((uint)alignment))
            throw new ArgumentException(
                "Alignment must be a power of two >= 8.",
                nameof(alignment)
            );

        if (_regions.Any(r => r.Tag == tag))
            throw new ArgumentException(
                $"Duplicate region tag '0x{tag:X8}' detected.",
                nameof(tag)
            );

        _regions.Add(new PlannedRegion(tag, byteCount, alignment));
        return this;
    }

    /// <summary>
    /// Adds a sub-partition typed to a strongly typed enum tag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedMemoryPlan AddRegion<TEnum>(TEnum tag, nuint byteCount, nuint alignment = 64)
        where TEnum : struct, Enum
    {
        uint rawTag = Unsafe.SizeOf<TEnum>() switch
        {
            4 => PlatformCompat.BitCast<TEnum, uint>(tag),
            2 => PlatformCompat.BitCast<TEnum, ushort>(tag),
            1 => PlatformCompat.BitCast<TEnum, byte>(tag),
            _ => (uint)Convert.ToInt64(tag),
        };

        return AddRegion(rawTag, byteCount, alignment);
    }

    /// <summary>Calculates the total byte size required for Page 0 plus all aligned partitions.</summary>
    public nuint CalculateTotalBytes()
    {
        // The first user partition begins at Page 1, isolating Page 0
        nuint cursor = AlignUp(SharedMemorySuperblock.SuperblockSize, PageBoundary);
        for (int i = 0; i < _regions.Count; i++)
        {
            cursor = AlignUp(cursor, _regions[i].Alignment);
            cursor += _regions[i].Length;
        }
        return AlignUp(cursor, 64);
    }

    internal unsafe void FormatSuperblock(
        SharedMemorySuperblock* sb,
        uint magic,
        uint abiVersion,
        ushort nodeId,
        nuint allocatedLength
    )
    {
        nuint totalRequired = CalculateTotalBytes();
        if (allocatedLength < totalRequired)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allocatedLength),
                $"Backing map length ({allocatedLength}B) is smaller than required layout ({totalRequired}B)."
            );
        }

        // Initialize metadata with State = Initializing
        sb->Magic = magic;
        sb->AbiVersion = abiVersion;
        sb->NodeId = nodeId;
        sb->RegionCount = (ushort)_regions.Count;
        sb->TotalByteLength = totalRequired;
        sb->CreatedTimestampTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        Volatile.Write(ref sb->State, (uint)SharedMemoryState.Initializing);

        SharedMemoryRegionEntry* entries = sb->GetRegionEntries();
        nuint cursor = AlignUp(SharedMemorySuperblock.SuperblockSize, PageBoundary);

        for (int i = 0; i < _regions.Count; i++)
        {
            cursor = AlignUp(cursor, _regions[i].Alignment);

            entries[i] = new SharedMemoryRegionEntry
            {
                Tag = _regions[i].Tag,
                Alignment = (uint)_regions[i].Alignment,
                Offset = cursor,
                Length = _regions[i].Length,
                Reserved = 0,
            };

            cursor += _regions[i].Length;
        }
    }

    /// <summary>Rounds <paramref name="value"/> up to the nearest multiple of power-of-two <paramref name="alignment"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignUp(nuint value, nuint alignment) =>
        (value + (alignment - 1)) & ~(alignment - 1);
}
