using Platform.Internal;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Posix.Enums;
using Platform.Native.Posix.Structs;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;
using Kernel32 = Platform.Native.Windows.Kernel32;
using Libc = Platform.Native.Posix.Libc;

namespace Platform.Memory;

public sealed unsafe partial class NativeMemoryMap
{
    #region Persistent File Factory Methods

    /// <summary>
    /// Exclusively creates a new file on disk, pre-allocates physical sectors, and maps it into virtual memory.
    /// Throws an <see cref="IOException"/> if the file already exists or allocation fails.
    /// </summary>
    public static NativeMemoryMap CreateNewFile(
        string filePath,
        nuint byteCount,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(filePath);
        PlatformCompat.ThrowIfZero(byteCount);

        NativeMapCreateStatus status = TryCreateNewFileCore(
            filePath,
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
                $"File '{filePath}' already exists."
            ),
            _ => throw new IOException(
                $"Failed to create persistent file mapping '{filePath}' ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
        };
    }

    /// <summary>
    /// Opens an existing file on disk and maps it into virtual memory.
    /// If <paramref name="byteCount"/> is 0, maps the entire file length.
    /// </summary>
    public static NativeMemoryMap OpenExistingFile(
        string filePath,
        nuint byteCount = 0,
        MemoryMapProtection protection = MemoryMapProtection.Read,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(filePath);

        NativeMapOpenStatus status = TryOpenExistingFileCore(
            filePath,
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
                $"File '{filePath}' was not found."
            ),
            NativeMapOpenStatus.InvalidSize => throw new IOException(
                $"File '{filePath}' size is smaller than requested mapping size."
            ),
            NativeMapOpenStatus.AccessDenied => throw new UnauthorizedAccessException(
                $"Access to file '{filePath}' was denied ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
            _ => throw new IOException(
                $"Failed to open persistent file mapping '{filePath}' ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
        };
    }

    /// <summary>
    /// Attempts to open an existing file on disk. Returns false if not found.
    /// Throws on access denial, schema/size mismatches, or system errors.
    /// </summary>
    public static bool TryOpenExistingFile(
        string filePath,
        nuint byteCount,
        MemoryMapProtection protection,
        MemoryMapOptions options,
        out NativeMemoryMap? map
    )
    {
        map = null;
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        NativeMapOpenStatus status = TryOpenExistingFileCore(
            filePath,
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
            NativeMapOpenStatus.InvalidSize => throw new IOException(
                $"File '{filePath}' size is smaller than requested mapping size."
            ),
            NativeMapOpenStatus.AccessDenied => throw new UnauthorizedAccessException(
                $"Access to file '{filePath}' was denied ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
            _ => throw new IOException(
                $"Failed to probe persistent file mapping '{filePath}' ({errorCode}): {PlatformCompat.GetErrorMessage(errorCode)}"
            ),
        };
    }

    /// <summary>
    /// Opens an existing file, or creates and pre-allocates it if it does not exist.
    /// </summary>
    public static NativeMemoryMap OpenOrCreateFile(
        string filePath,
        nuint byteCount,
        MemoryMapProtection protection = MemoryMapProtection.ReadWrite,
        MemoryMapOptions options = MemoryMapOptions.None
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(filePath);
        PlatformCompat.ThrowIfZero(byteCount);

        NativeMapCreateStatus createStatus = TryCreateNewFileCore(
            filePath,
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
                $"Failed to create persistent file '{filePath}' ({createError}): {PlatformCompat.GetErrorMessage(createError)}"
            );
        }

        return OpenExistingFile(filePath, byteCount, protection, options);
    }

    #endregion

    #region Internal File Core Implementation

    private static NativeMapCreateStatus TryCreateNewFileCore(
        string filePath,
        nuint byteCount,
        MemoryMapProtection protection,
        MemoryMapOptions options,
        out NativeMemoryMap? map,
        out int errorCode
    )
    {
        map = null;
        errorCode = 0;
        string fullPath = Path.GetFullPath(filePath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            PosixOpenFlags oflags =
                PosixOpenFlags.Create | PosixOpenFlags.Exclusive | PosixOpenFlags.ReadWrite;
            int fd = Libc.Open(fullPath, oflags, _defaultPosixShmAndFileMode);
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
                // Guarantee physical blocks are allocated up front to avoid runtime SIGBUS on disk exhaustion
                int allocateResult = Libc.Posix_Fallocate(fd, 0, (nint)byteCount);
                if (allocateResult != 0)
                {
                    errorCode = allocateResult;
                    return NativeMapCreateStatus.SystemError;
                }

                MemoryMapProt prot = TranslatePosixProt(protection);
                MemoryMapFlags mapFlags = MemoryMapFlags.Shared;

                if ((options & MemoryMapOptions.PreFault) != 0)
                {
                    mapFlags |= MemoryMapFlags.Populate;
                }

                ptr = Libc.Mmap(null, byteCount, prot, mapFlags, fd, 0);
                if (ptr == Libc.MAP_FAILED)
                {
                    ptr = null;
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                // Keep file descriptor open so Flush(flushToDisk: true) can call fdatasync
                map = new NativeMemoryMap(ptr, byteCount, fileDescriptor: fd, ownsShm: false);
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

                    Libc.Close(fd);
                }
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FileAccessRights desiredAccess = FileAccessRights.GenericRead;
            if ((protection & MemoryMapProtection.Write) != 0)
            {
                desiredAccess |= FileAccessRights.GenericWrite;
            }

            // Include Delete sharing to avoid sharing violations during cleanup/tests
            FileShareMode shareMode =
                FileShareMode.Read | FileShareMode.Write | FileShareMode.Delete;

            nint hFile = Kernel32.CreateFileW(
                fullPath,
                desiredAccess,
                shareMode,
                null,
                CreationDisposition.CreateNew,
                FileAttributesFlags.Normal,
                0
            );

            if (hFile == Kernel32.INVALID_HANDLE_VALUE || hFile == 0)
            {
                errorCode = PlatformCompat.GetLastError();
                return
                    (uint)errorCode == Win32Errors.ERROR_FILE_EXISTS
                    || (uint)errorCode == Win32Errors.ERROR_ALREADY_EXISTS
                    ? NativeMapCreateStatus.AlreadyExists
                    : NativeMapCreateStatus.SystemError;
            }

            bool success = false;
            nint hMap = 0;
            void* ptr = null;
            try
            {
                // Explicitly grow/preallocate the physical file on disk
                if (
                    !Kernel32.SetFilePointerEx(
                        hFile,
                        (long)byteCount,
                        out _,
                        0 /* FILE_BEGIN */
                    ) || !Kernel32.SetEndOfFile(hFile)
                )
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                // Reset pointer back to the beginning
                Kernel32.SetFilePointerEx(
                    hFile,
                    0,
                    out _,
                    0 /* FILE_BEGIN */
                );

                PageProtection flProtect = TranslateWin32Protect(protection);
                uint high = (uint)(byteCount >> _shiftDwordHigh);
                uint low = (uint)(byteCount & _maskDwordLow);

                hMap = Kernel32.CreateFileMappingW(
                    hFile,
                    null,
                    (uint)flProtect | (uint)SectionAttributes.Commit,
                    high,
                    low,
                    null
                );

                if (hMap == 0)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                FileMapAccess viewAccess = TranslateWin32DesiredAccess(protection);
                ptr = Kernel32.MapViewOfFile(hMap, viewAccess, 0, 0, byteCount);
                if (ptr == null)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapCreateStatus.SystemError;
                }

                map = new NativeMemoryMap(
                    ptr,
                    byteCount,
                    windowsHandle: hMap,
                    windowsFileHandle: hFile
                );
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

                    if (hMap != 0)
                    {
                        Kernel32.CloseHandle(hMap);
                    }

                    Kernel32.CloseHandle(hFile);
                }
            }
        }

        errorCode = _genericSystemErrorCode;
        return NativeMapCreateStatus.SystemError;
    }

    private static NativeMapOpenStatus TryOpenExistingFileCore(
        string filePath,
        nuint byteCount,
        MemoryMapProtection protection,
        MemoryMapOptions options,
        out NativeMemoryMap? map,
        out int errorCode
    )
    {
        map = null;
        errorCode = 0;
        string fullPath = Path.GetFullPath(filePath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            PosixOpenFlags oflags =
                (protection == MemoryMapProtection.Read)
                    ? PosixOpenFlags.ReadOnly
                    : PosixOpenFlags.ReadWrite;

            int fd = Libc.Open(fullPath, oflags, _posixOpenExistingMode);
            if (fd < 0)
            {
                errorCode = PlatformCompat.GetLastError();
                if (errorCode == PosixErrors.ENOENT)
                    return NativeMapOpenStatus.NotFound;
                if (errorCode is PosixErrors.EACCES or PosixErrors.EPERM)
                    return NativeMapOpenStatus.AccessDenied;

                return NativeMapOpenStatus.SystemError;
            }

            bool success = false;
            void* ptr = null;
            try
            {
                if (Libc.Fstat(fd, out Stat stat) != 0)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.SystemError;
                }

                nuint fileSize = (nuint)stat.st_size;
                if (fileSize == 0)
                {
                    errorCode = PosixErrors.EINVAL;
                    return NativeMapOpenStatus.InvalidSize;
                }

                nuint mappingLength = byteCount == 0 ? fileSize : byteCount;
                if (fileSize < mappingLength)
                {
                    errorCode = PosixErrors.EINVAL;
                    return NativeMapOpenStatus.InvalidSize;
                }

                MemoryMapProt prot = TranslatePosixProt(protection);
                MemoryMapFlags mapFlags = MemoryMapFlags.Shared;

                if ((options & MemoryMapOptions.PreFault) != 0)
                {
                    mapFlags |= MemoryMapFlags.Populate;
                }

                ptr = Libc.Mmap(null, mappingLength, prot, mapFlags, fd, 0);
                if (ptr == Libc.MAP_FAILED)
                {
                    ptr = null;
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.SystemError;
                }

                map = new NativeMemoryMap(ptr, mappingLength, fileDescriptor: fd, ownsShm: false);
                ApplyPosixAdvice(ptr, mappingLength, options);
                success = true;
                return NativeMapOpenStatus.Success;
            }
            finally
            {
                if (!success)
                {
                    if (ptr != null && ptr != Libc.MAP_FAILED)
                    {
                        Libc.Munmap(ptr, byteCount);
                    }

                    Libc.Close(fd);
                }
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FileAccessRights desiredAccess = FileAccessRights.GenericRead;
            if ((protection & MemoryMapProtection.Write) != 0)
            {
                desiredAccess |= FileAccessRights.GenericWrite;
            }

            FileShareMode shareMode = FileShareMode.Read | FileShareMode.Write;

            nint hFile = Kernel32.CreateFileW(
                fullPath,
                desiredAccess,
                shareMode,
                null,
                CreationDisposition.OpenExisting,
                FileAttributesFlags.Normal,
                0
            );

            if (hFile == Kernel32.INVALID_HANDLE_VALUE || hFile == 0)
            {
                errorCode = PlatformCompat.GetLastError();
                if ((uint)errorCode == Win32Errors.ERROR_FILE_NOT_FOUND)
                    return NativeMapOpenStatus.NotFound;
                if ((uint)errorCode == Win32Errors.ERROR_ACCESS_DENIED)
                    return NativeMapOpenStatus.AccessDenied;

                return NativeMapOpenStatus.SystemError;
            }

            bool success = false;
            nint hMap = 0;
            void* ptr = null;
            try
            {
                if (!Kernel32.GetFileSizeEx(hFile, out long fileSizeRaw) || fileSizeRaw <= 0)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.InvalidSize;
                }

                nuint fileSize = (nuint)fileSizeRaw;
                nuint mappingLength = byteCount == 0 ? fileSize : byteCount;

                if (fileSize < mappingLength)
                {
                    return NativeMapOpenStatus.InvalidSize;
                }

                PageProtection flProtect = TranslateWin32Protect(protection);
                uint high = (uint)(mappingLength >> _shiftDwordHigh);
                uint low = (uint)(mappingLength & _maskDwordLow);

                hMap = Kernel32.CreateFileMappingW(
                    hFile,
                    null,
                    (uint)flProtect | (uint)SectionAttributes.Commit,
                    high,
                    low,
                    null
                );

                if (hMap == 0)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.SystemError;
                }

                FileMapAccess viewAccess = TranslateWin32DesiredAccess(protection);
                ptr = Kernel32.MapViewOfFile(hMap, viewAccess, 0, 0, mappingLength);
                if (ptr == null)
                {
                    errorCode = PlatformCompat.GetLastError();
                    return NativeMapOpenStatus.SystemError;
                }

                map = new NativeMemoryMap(
                    ptr,
                    mappingLength,
                    windowsHandle: hMap,
                    windowsFileHandle: hFile
                );
                ApplyWindowsPrefetch(ptr, mappingLength, options);
                success = true;
                return NativeMapOpenStatus.Success;
            }
            finally
            {
                if (!success)
                {
                    if (ptr != null)
                    {
                        Kernel32.UnmapViewOfFile(ptr);
                    }

                    if (hMap != 0)
                    {
                        Kernel32.CloseHandle(hMap);
                    }

                    Kernel32.CloseHandle(hFile);
                }
            }
        }

        errorCode = _genericSystemErrorCode;
        return NativeMapOpenStatus.SystemError;
    }

    #endregion
}
