namespace Platform.Memory;

[Flags]
public enum MemoryMapProtection : byte
{
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
    Execute = 4,
    ReadWriteExecute = Read | Write | Execute,
}
