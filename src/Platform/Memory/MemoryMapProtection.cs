namespace Platform.Memory;

/// <summary>
/// Virtual memory page protection flags for <see cref="NativeMemoryMap"/>.
/// </summary>
[Flags]
public enum MemoryMapProtection : byte
{
    /// <summary>Pages can be read.</summary>
    Read = 1,

    /// <summary>Pages can be written.</summary>
    Write = 2,

    /// <summary>Pages can be read and written.</summary>
    ReadWrite = Read | Write,

    /// <summary>Pages can be executed.</summary>
    Execute = 4,

    /// <summary>Pages can be read, written, and executed.</summary>
    ReadWriteExecute = Read | Write | Execute,
}
