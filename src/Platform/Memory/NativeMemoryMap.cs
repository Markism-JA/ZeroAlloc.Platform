using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Internal;
using Platform.Native.Posix;
using Platform.Native.Posix.Enums;
using Platform.Native.Posix.Structs;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;
using Platform.Native.Windows.Structs;
using Kernel32 = Platform.Native.Windows.Kernel32;
using Libc = Platform.Native.Posix.Libc;

// ReSharper disable NullableWarningSuppressionIsUsed

namespace Platform.Memory;

/// <summary>
/// High-performance, zero-allocation native memory-mapped region.
/// Supports:
/// <list type="bullet">
///     <item><description>Linux (POSIX mmap, /dev/shm)</description></item>
///     <item><description>Windows (CreateFileMapping, MapViewOfFile)</description></item>
/// </list>
/// </summary>
/// <remarks>
/// This primitive is fundamentally unsafe.
/// Thread-safe for independent mapping operations, but <see cref="Dispose"/> must not
/// race with consumers using <see cref="Pointer"/>, <see cref="AsSpan()"/>, <see cref="AsSpan{T}(int, int)"/>, or <see cref="Flush"/>.
/// Point-in-time disposal checks have been omitted from the hot path by design.
/// The caller exclusively owns the lifetime relationship and synchronization between the mapping and its views.
/// </remarks>
public sealed unsafe partial class NativeMemoryMap : IDisposable
{
    /// <summary>Default timeout in seconds when waiting for a peer to finish initializing a shared segment.</summary>
    public const int DefaultHandshakeTimeoutSeconds = 5;

    private const int _anonymousFd = -1;
    private const int _posixOpenExistingMode = 0;
    private const int _defaultPosixShmAndFileMode = 0b110_110_000; // 0660: rw-rw----
    private const int _genericSystemErrorCode = -1;

    private void* _pointer;
    private readonly nuint _length;
    private readonly nint _windowsHandle; // CreateFileMappingW handle
    private readonly nint _windowsFileHandle; // CreateFileW handle (persistent files)
    private readonly int _fileDescriptor;
    private readonly string? _shmName;
    private readonly bool _ownsShm;
    private int _isDisposed;

    /// <summary>
    /// Gets the raw, unmanaged base pointer to the mapped virtual address range.
    /// The caller is strictly responsible for ensuring the mapping is not disposed while in use.
    /// </summary>
    public byte* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte*)_pointer;
    }

    /// <summary>
    /// Total capacity in bytes of this mapped region.
    /// </summary>
    public nuint Length => _length;

    private NativeMemoryMap(
        void* pointer,
        nuint length,
        nint windowsHandle = 0,
        nint windowsFileHandle = 0,
        int fileDescriptor = -1,
        string? shmName = null,
        bool ownsShm = false
    )
    {
        _pointer = pointer;
        _length = length;
        _windowsHandle = windowsHandle;
        _windowsFileHandle = windowsFileHandle;
        _fileDescriptor = fileDescriptor;
        _shmName = shmName;
        _ownsShm = ownsShm;
    }

    #region Anonymous Mappings

    /// <summary>
    /// Creates an anonymous memory map (backed strictly by RAM/swap, zero filesystem overhead).
    /// </summary>
    public static NativeMemoryMap CreateAnonymous(
        nuint byteCount,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.PreFault
    )
    {
        PlatformCompat.ThrowIfZero(byteCount);

        if (PosixAbi.IsPosix)
        {
            MemoryMapProt prot = TranslatePosixProt(protection);
            MemoryMapFlags flags = PosixAbi.WithPreFault(
                PosixAbi.AnonymousMapFlag | MemoryMapFlags.Private,
                options
            );

            void* ptr = Libc.Mmap(null, byteCount, prot, flags, _anonymousFd, 0);
            if (ptr == Libc.MAP_FAILED)
            {
                int error = PlatformCompat.GetLastError();
                throw new IOException(
                    $"POSIX mmap anonymous failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                );
            }

            var map = new NativeMemoryMap(ptr, byteCount);
            ApplyPosixAdvice(ptr, byteCount, options);
            return map;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            PageProtection flProtect = TranslateWin32Protect(protection);
            var (high, low) = SplitSize64(byteCount);

            nint hMap = Kernel32.CreateFileMappingW(
                Kernel32.INVALID_HANDLE_VALUE,
                null,
                (uint)flProtect | (uint)SectionAttributes.Commit,
                high,
                low,
                null
            );

            if (hMap == 0)
            {
                int error = PlatformCompat.GetLastError();
                throw new Win32Exception(
                    error,
                    $"Windows CreateFileMapping failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                );
            }

            bool success = false;
            void* ptr = null;
            try
            {
                FileMapAccess desiredAccess = TranslateWin32DesiredAccess(protection);
                ptr = Kernel32.MapViewOfFile(hMap, desiredAccess, 0, 0, byteCount);
                if (ptr == null)
                {
                    int error = PlatformCompat.GetLastError();
                    throw new Win32Exception(
                        error,
                        $"Windows MapViewOfFile failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                    );
                }

                var map = new NativeMemoryMap(ptr, byteCount, windowsHandle: hMap);
                ApplyWindowsPrefetch(ptr, byteCount, options);
                success = true;
                return map;
            }
            finally
            {
                if (!success)
                {
                    if (ptr != null)
                    {
                        Kernel32.UnmapViewOfFile(ptr);
                    }
                    Kernel32.CloseHandle(hMap);
                }
            }
        }

        throw new PlatformNotSupportedException("Operating system is not supported.");
    }

    #endregion

    #region Shared Memory Factory Methods

    /// <summary>
    /// Exclusively creates a new shared memory segment.
    /// Throws an <see cref="IOException"/> if the segment already exists or creation fails.
    /// </summary>
    public static NativeMemoryMap CreateNewShared(
        string name,
        nuint byteCount,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.PreFault
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(name);
        PlatformCompat.ThrowIfZero(byteCount);

        NativeMapCreateStatus status = TryCreateNewSharedCore(
            name,
            byteCount,
            protection,
            options,
            out NativeMemoryMap? map,
            out int errorCode
        );

        return status switch
        {
            NativeMapCreateStatus.Success => map!,
            NativeMapCreateStatus.AlreadyExists => throw new IOException(
                $"Shared memory segment '{name}' already exists."
            ),
            _ => throw new IOException(
                $"Failed to create shared memory segment '{name}' ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
        };
    }

    /// <summary>
    /// Opens an existing shared memory segment.
    /// Fails with a descriptive exception if the segment does not exist, permission is denied, or the mapping fails.
    /// </summary>
    public static NativeMemoryMap OpenExistingShared(
        string name,
        nuint byteCount,
        MemoryMapProtection protection = MemoryMapProtection.Read,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(name);
        PlatformCompat.ThrowIfZero(byteCount);

        NativeMapOpenStatus status = TryOpenExistingSharedCore(
            name,
            byteCount,
            protection,
            options,
            out NativeMemoryMap? map,
            out int errorCode
        );

        return status switch
        {
            NativeMapOpenStatus.Success => map!,
            NativeMapOpenStatus.NotFound => throw new FileNotFoundException(
                $"Shared memory segment '{name}' does not exist or has not been initialized."
            ),
            NativeMapOpenStatus.NotInitialized => throw new IOException(
                $"Shared memory segment '{name}' exists but is uninitialized (size 0)."
            ),
            NativeMapOpenStatus.InvalidSize => throw new IOException(
                $"Shared memory segment '{name}' is smaller than requested."
            ),
            NativeMapOpenStatus.AccessDenied => throw new UnauthorizedAccessException(
                $"Access to shared memory segment '{name}' was denied ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
            _ => throw new IOException(
                $"Failed to open shared memory segment '{name}' ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
        };
    }

    /// <summary>
    /// Attempts to open an existing shared memory segment.
    /// Returns false if not found. Throws on access denial, permanent sizing mismatches, or OS resource failures.
    /// </summary>
    public static bool TryOpenExistingShared(
        string name,
        nuint byteCount,
        MemoryMapProtection protection,
        MemoryMapOptions options,
        out NativeMemoryMap? map
    )
    {
        map = null;
        if (string.IsNullOrWhiteSpace(name) || byteCount == 0)
            return false;

        NativeMapOpenStatus status = TryOpenExistingSharedCore(
            name,
            byteCount,
            protection,
            options,
            out map,
            out int errorCode
        );

        return status switch
        {
            NativeMapOpenStatus.Success => true,
            NativeMapOpenStatus.NotFound => false,
            NativeMapOpenStatus.NotInitialized => throw new IOException(
                $"Shared memory segment '{name}' exists but is uninitialized (size 0)."
            ),
            NativeMapOpenStatus.InvalidSize => throw new IOException(
                $"Shared memory segment '{name}' is smaller than requested."
            ),
            NativeMapOpenStatus.AccessDenied => throw new UnauthorizedAccessException(
                $"Access to shared memory segment '{name}' was denied ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
            _ => throw new IOException(
                $"Failed to probe shared memory segment '{name}' ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
        };
    }

    /// <summary>
    /// Thread-safe and process-safe pipeline: Attempts to create the map exclusively.
    /// If it already exists, spins until the creator completes sizing and maps the view.
    /// </summary>
    public static NativeMemoryMap OpenOrCreateShared(
        string name,
        nuint byteCount,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.PreFault,
        TimeSpan timeout = default
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(name);
        PlatformCompat.ThrowIfZero(byteCount);

        NativeMapCreateStatus createStatus = TryCreateNewSharedCore(
            name,
            byteCount,
            protection,
            options,
            out NativeMemoryMap? map,
            out int createError
        );

        if (createStatus == NativeMapCreateStatus.Success)
            return map!;

        if (createStatus != NativeMapCreateStatus.AlreadyExists)
        {
            throw new IOException(
                $"Failed to create shared memory segment '{name}' ({createError}): {PlatformCompat.GetErrorMessage(createError)}"
            );
        }

        SpinWait spinner = default;
        long timeoutTicks =
            timeout == default
                ? Stopwatch.Frequency * DefaultHandshakeTimeoutSeconds
                : (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        long deadline = Stopwatch.GetTimestamp() + timeoutTicks;

        while (true)
        {
            NativeMapOpenStatus openStatus = TryOpenExistingSharedCore(
                name,
                byteCount,
                protection,
                options,
                out map,
                out int openError
            );

            switch (openStatus)
            {
                case NativeMapOpenStatus.Success:
                    return map!;

                case NativeMapOpenStatus.NotInitialized:
                    if (Stopwatch.GetTimestamp() >= deadline)
                    {
                        throw new TimeoutException(
                            $"Timed out waiting for shared memory segment '{name}' to become initialized (ftruncate timeout)."
                        );
                    }
                    spinner.SpinOnce();
                    continue;

                case NativeMapOpenStatus.NotFound:
                    // The creator failed and unlinked it before sizing, or tore it down.
                    // Pivot back to attempting exclusive creation.
                    createStatus = TryCreateNewSharedCore(
                        name,
                        byteCount,
                        protection,
                        options,
                        out map,
                        out createError
                    );

                    if (createStatus == NativeMapCreateStatus.Success)
                        return map!;

                    if (createStatus != NativeMapCreateStatus.AlreadyExists)
                    {
                        throw new IOException(
                            $"Failed to recreate shared memory segment '{name}' ({createError}): {PlatformCompat.GetErrorMessage(createError)}"
                        );
                    }
                    continue;

                case NativeMapOpenStatus.AccessDenied:
                    if (PosixAbi.IsMacOS && Stopwatch.GetTimestamp() < deadline)
                    {
                        spinner.SpinOnce();
                        continue;
                    }
                    throw new UnauthorizedAccessException(
                        $"Access to shared memory segment '{name}' was denied ({openError}): {PlatformCompat.GetErrorMessage(openError)}"
                    );

                case NativeMapOpenStatus.InvalidSize:
                    throw new IOException(
                        $"Shared memory segment '{name}' is permanently smaller than requested."
                    );

                case NativeMapOpenStatus.SystemError:
                default:
                    throw new IOException(
                        $"Failed to open shared memory segment '{name}' ({openError}): {PlatformCompat.GetErrorMessage(openError)}"
                    );
            }
        }
    }

    /// <summary>
    /// Unlinks/removes a POSIX shared memory object. Does nothing on Windows (which reclaims on handle close).
    /// </summary>
    public static void UnlinkShared(string name)
    {
        if (PosixAbi.IsPosix)
        {
            PosixAbi.UnlinkShared(PosixAbi.GetNormalizedSharedPath(name));
        }
    }

    #endregion

    #region Span Views & Operations

    /// <summary>
    /// Gets a mutable Span over the mapped memory region.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> AsSpan()
    {
        PlatformCompat.ThrowIfGreaterThan(_length, int.MaxValue, nameof(Length));
        return new Span<byte>(Pointer, (int)_length);
    }

    /// <summary>
    /// Gets a read-only Span view over the mapped memory region.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsReadOnlySpan()
    {
        PlatformCompat.ThrowIfGreaterThan(_length, int.MaxValue, nameof(Length));
        return new ReadOnlySpan<byte>(Pointer, (int)_length);
    }

    /// <summary>
    /// Gets a typed Span view over an unmanaged data segment.
    /// The caller is exclusively responsible for ensuring <paramref name="offsetBytes"/> satisfies the alignment requirements of <typeparamref name="T"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan<T>(int offsetBytes, int count)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);
        PlatformCompat.ThrowIfNegative(count);

        nuint requiredBytes = (nuint)count * (nuint)sizeof(T);
        nuint totalEnd = (nuint)offsetBytes + requiredBytes;

        if (totalEnd > _length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"Span window [{offsetBytes}..{totalEnd}] exceeds map boundary of {_length} bytes."
            );
        }

        return new Span<T>(Pointer + offsetBytes, count);
    }

    /// <summary>
    /// Requests synchronization of modified mapped pages with backing storage.
    /// </summary>
    /// <param name="flushToDisk">
    /// When true, forces physical drive controllers to commit data to non-volatile storage (fdatasync / FlushFileBuffers).
    /// When false, flushes only to the operating system's page cache (msync / FlushViewOfFile).
    /// </param>
    public void Flush(bool flushToDisk = false)
    {
        if (PosixAbi.IsPosix)
        {
            if (Libc.Msync(_pointer, _length, PosixAbi.MsyncSyncFlag) != 0)
            {
                int error = PlatformCompat.GetLastError();
                throw new IOException(
                    $"POSIX msync failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                );
            }

            if (flushToDisk && _fileDescriptor >= 0)
            {
                if (PosixAbi.FlushDataSync(_fileDescriptor) != 0)
                {
                    int error = PlatformCompat.GetLastError();
                    throw new IOException(
                        $"POSIX data sync failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                    );
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!Kernel32.FlushViewOfFile(_pointer, _length))
            {
                int error = PlatformCompat.GetLastError();
                throw new Win32Exception(
                    error,
                    $"Windows FlushViewOfFile failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                );
            }

            if (
                flushToDisk
                && _windowsFileHandle != 0
                && _windowsFileHandle != Kernel32.INVALID_HANDLE_VALUE
            )
            {
                if (!Kernel32.FlushFileBuffers(_windowsFileHandle))
                {
                    int error = PlatformCompat.GetLastError();
                    throw new Win32Exception(
                        error,
                        $"Windows FlushFileBuffers failed ({error}): {PlatformCompat.GetErrorMessage(error)}"
                    );
                }
            }
        }
    }

    #endregion

    #region Internal Core Factories

    private static NativeMapCreateStatus TryCreateNewSharedCore(
        string name,
        nuint byteCount,
        MemoryMapProtection protection,
        MemoryMapOptions options,
        out NativeMemoryMap? map,
        out int errorCode
    )
    {
        map = null;
        errorCode = 0;

        if (PosixAbi.IsPosix)
        {
            string shmPath = PosixAbi.GetNormalizedSharedPath(name);
            PosixOpenFlags oflags =
                PosixOpenFlags.Create | PosixOpenFlags.Exclusive | PosixOpenFlags.ReadWrite;

            int fd = PosixAbi.OpenShared(shmPath, oflags, _defaultPosixShmAndFileMode);
            if (fd < 0)
            {
                errorCode = PlatformCompat.GetLastError();
                return errorCode == PosixErrors.EEXIST
                    ? NativeMapCreateStatus.AlreadyExists
                    : NativeMapCreateStatus.SystemError;
            }

            bool success = false;
            void* ptr = null;
            try
            {
                if (Libc.Ftruncate(fd, (nint)byteCount) != 0)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                MemoryMapProt prot = TranslatePosixProt(protection);
                MemoryMapFlags mapFlags = PosixAbi.WithPreFault(MemoryMapFlags.Shared, options);

                ptr = Libc.Mmap(null, byteCount, prot, mapFlags, fd, 0);
                if (ptr == Libc.MAP_FAILED)
                {
                    ptr = null;
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                map = new NativeMemoryMap(ptr, byteCount, shmName: shmPath, ownsShm: true);
                ApplyPosixAdvice(ptr, byteCount, options);
                success = true;
                return NativeMapCreateStatus.Success;
            }
            finally
            {
                if (!success)
                {
                    if (ptr != null && ptr != Libc.MAP_FAILED)
                    {
                        Libc.Munmap(ptr, byteCount);
                    }
                    PosixAbi.UnlinkShared(shmPath);
                }
                Libc.Close(fd);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string winMapName = NormalizeWindowsMappingName(name);
            PageProtection flProtect = TranslateWin32Protect(protection);
            var (high, low) = SplitSize64(byteCount);

            nint hMap = Kernel32.CreateFileMappingW(
                Kernel32.INVALID_HANDLE_VALUE,
                null,
                (uint)flProtect | (uint)SectionAttributes.Commit,
                high,
                low,
                winMapName
            );

            errorCode = PlatformCompat.GetLastError();
            if (hMap == 0)
            {
                return NativeMapCreateStatus.SystemError;
            }

            if (errorCode == (int)Win32Errors.ERROR_ALREADY_EXISTS)
            {
                Kernel32.CloseHandle(hMap);
                return NativeMapCreateStatus.AlreadyExists;
            }

            bool success = false;
            void* ptr = null;
            try
            {
                FileMapAccess desiredAccess = TranslateWin32DesiredAccess(protection);
                ptr = Kernel32.MapViewOfFile(hMap, desiredAccess, 0, 0, byteCount);
                if (ptr == null)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                map = new NativeMemoryMap(ptr, byteCount, windowsHandle: hMap);
                ApplyWindowsPrefetch(ptr, byteCount, options);
                success = true;
                return NativeMapCreateStatus.Success;
            }
            finally
            {
                if (!success)
                {
                    if (ptr != null)
                    {
                        Kernel32.UnmapViewOfFile(ptr);
                    }
                    Kernel32.CloseHandle(hMap);
                }
            }
        }

        errorCode = _genericSystemErrorCode;
        return NativeMapCreateStatus.SystemError;
    }

    private static NativeMapOpenStatus TryOpenExistingSharedCore(
        string name,
        nuint byteCount,
        MemoryMapProtection protection,
        MemoryMapOptions options,
        out NativeMemoryMap? map,
        out int errorCode
    )
    {
        map = null;
        errorCode = 0;

        if (PosixAbi.IsPosix)
        {
            string shmPath = PosixAbi.GetNormalizedSharedPath(name);
            PosixOpenFlags oflags =
                (protection == MemoryMapProtection.Read)
                    ? PosixOpenFlags.ReadOnly
                    : PosixOpenFlags.ReadWrite;

            int fd = PosixAbi.OpenShared(shmPath, oflags, _posixOpenExistingMode);
            if (fd < 0)
            {
                errorCode = PlatformCompat.GetLastError();

                if (errorCode == PosixErrors.ENOENT)
                    return NativeMapOpenStatus.NotFound;
                if (errorCode is PosixErrors.EACCES or PosixErrors.EPERM)
                    return NativeMapOpenStatus.AccessDenied;

                return NativeMapOpenStatus.SystemError;
            }

            try
            {
                if (!PosixAbi.TryGetFileSize(fd, out long fileSize))
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.SystemError;
                }

                if (fileSize == 0)
                {
                    return NativeMapOpenStatus.NotInitialized;
                }

                if ((nuint)fileSize < byteCount)
                {
                    errorCode = PosixErrors.EINVAL;
                    return NativeMapOpenStatus.InvalidSize;
                }

                MemoryMapProt prot = TranslatePosixProt(protection);
                MemoryMapFlags mapFlags = PosixAbi.WithPreFault(MemoryMapFlags.Shared, options);

                void* ptr = Libc.Mmap(null, byteCount, prot, mapFlags, fd, 0);
                if (ptr == Libc.MAP_FAILED)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.SystemError;
                }

                map = new NativeMemoryMap(ptr, byteCount, shmName: shmPath, ownsShm: false);
                ApplyPosixAdvice(ptr, byteCount, options);
                return NativeMapOpenStatus.Success;
            }
            finally
            {
                Libc.Close(fd);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string winMapName = NormalizeWindowsMappingName(name);
            FileMapAccess desiredAccess = TranslateWin32DesiredAccess(protection);

            nint hMap = Kernel32.OpenFileMappingW(desiredAccess, false, winMapName);
            if (hMap == 0)
            {
                errorCode = PlatformCompat.GetLastError();

                if (errorCode == (int)Win32Errors.ERROR_FILE_NOT_FOUND)
                    return NativeMapOpenStatus.NotFound;
                if (errorCode == (int)Win32Errors.ERROR_ACCESS_DENIED)
                    return NativeMapOpenStatus.AccessDenied;

                return NativeMapOpenStatus.SystemError;
            }

            void* ptr = null;
            try
            {
                ptr = Kernel32.MapViewOfFile(hMap, desiredAccess, 0, 0, byteCount);
                if (ptr == null)
                {
                    errorCode = PlatformCompat.GetLastError();

                    if (
                        errorCode == (int)Win32Errors.ERROR_NOT_ENOUGH_MEMORY
                        || errorCode == (int)Win32Errors.ERROR_INVALID_PARAMETER
                    )
                    {
                        return NativeMapOpenStatus.InvalidSize;
                    }

                    return NativeMapOpenStatus.SystemError;
                }

                map = new NativeMemoryMap(ptr, byteCount, windowsHandle: hMap);
                ApplyWindowsPrefetch(ptr, byteCount, options);
                return NativeMapOpenStatus.Success;
            }
            finally
            {
                if (map == null)
                {
                    if (ptr != null)
                    {
                        Kernel32.UnmapViewOfFile(ptr);
                    }
                    Kernel32.CloseHandle(hMap);
                }
            }
        }

        errorCode = _genericSystemErrorCode;
        return NativeMapOpenStatus.SystemError;
    }

    private static string NormalizePosixShmName(string name) =>
        name.StartsWith('/') ? name : "/" + name;

    private static string NormalizeWindowsMappingName(string name)
    {
        string sanitized = name.TrimStart('/').Replace('/', '_');
        return
            sanitized.StartsWith("Local\\", StringComparison.OrdinalIgnoreCase)
            || sanitized.StartsWith("Global\\", StringComparison.OrdinalIgnoreCase)
            ? sanitized
            : "Local\\" + sanitized;
    }

    private static void ApplyPosixAdvice(void* ptr, nuint length, MemoryMapOptions options)
    {
        if ((options & MemoryMapOptions.Sequential) != 0)
            Libc.Madvise(ptr, length, MadviseAdvice.Sequential);
        if ((options & MemoryMapOptions.RandomAccess) != 0)
            Libc.Madvise(ptr, length, MadviseAdvice.Random);
        if (PosixAbi.IsMacOS && (options & MemoryMapOptions.PreFault) != 0)
            Libc.Madvise(ptr, length, MadviseAdvice.WillNeed);
    }

    private static void ApplyWindowsPrefetch(void* ptr, nuint length, MemoryMapOptions options)
    {
        if ((options & MemoryMapOptions.PreFault) != 0)
        {
            var range = new Win32MemoryRangeEntry { VirtualAddress = ptr, NumberOfBytes = length };

            if (!Kernel32.PrefetchVirtualMemory(Kernel32.GetCurrentProcess(), 1, &range, 0))
            {
                byte* p = (byte*)ptr;
                int pageSize = Environment.SystemPageSize;
                for (nuint i = 0; i < length; i += (nuint)pageSize)
                {
                    _ = Volatile.Read(ref p[i]);
                }
            }
        }
    }

    private static MemoryMapProt TranslatePosixProt(MemoryMapProtection protection)
    {
        MemoryMapProt prot = MemoryMapProt.None;
        if ((protection & MemoryMapProtection.Read) != 0)
            prot |= MemoryMapProt.Read;
        if ((protection & MemoryMapProtection.Write) != 0)
            prot |= MemoryMapProt.Write;
        if ((protection & MemoryMapProtection.Execute) != 0)
            prot |= MemoryMapProt.Execute;
        return prot;
    }

    private static PageProtection TranslateWin32Protect(MemoryMapProtection protection)
    {
        if ((protection & MemoryMapProtection.Execute) != 0)
        {
            if ((protection & MemoryMapProtection.Write) != 0)
                return PageProtection.ExecuteReadWrite;
            return PageProtection.ExecuteRead;
        }

        if ((protection & MemoryMapProtection.Write) != 0)
            return PageProtection.ReadWrite;
        return PageProtection.ReadOnly;
    }

    private static FileMapAccess TranslateWin32DesiredAccess(MemoryMapProtection protection)
    {
        FileMapAccess access = 0;
        if ((protection & MemoryMapProtection.Execute) != 0)
            access |= FileMapAccess.Execute;
        if ((protection & MemoryMapProtection.Write) != 0)
            access |= FileMapAccess.Write;
        if ((protection & MemoryMapProtection.Read) != 0)
            access |= FileMapAccess.Read;
        return access;
    }

    private static (uint High, uint Low) SplitSize64(ulong byteCount) =>
        ((uint)(byteCount >> 32), (uint)byteCount);

    #endregion

    /// <summary>
    /// Unmaps the virtual memory view, closes OS handles/file descriptors, and unlinks owner-created shared IPC segments.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        if (_pointer != null)
        {
            if (PosixAbi.IsPosix)
            {
                if (_pointer != Libc.MAP_FAILED)
                {
                    Libc.Munmap(_pointer, _length);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Kernel32.UnmapViewOfFile(_pointer);
            }

            _pointer = null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (_windowsHandle != 0)
            {
                Kernel32.CloseHandle(_windowsHandle);
            }

            if (_windowsFileHandle != 0 && _windowsFileHandle != Kernel32.INVALID_HANDLE_VALUE)
            {
                Kernel32.CloseHandle(_windowsFileHandle);
            }
        }

        if (PosixAbi.IsPosix)
        {
            if (_fileDescriptor >= 0)
            {
                Libc.Close(_fileDescriptor);
            }

            // Only shared IPC memory unlinks on dispose; persistent files never unlink.
            if (_ownsShm && _shmName != null)
            {
                PosixAbi.UnlinkShared(_shmName);
            }
        }
    }
}
