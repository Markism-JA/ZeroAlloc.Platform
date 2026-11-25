namespace Platform.Memory.Shared;

public enum SharedMemoryState : uint
{
    Uninitialized = 0,
    Initializing = 1,
    Ready = 2,
    Faulted = 3,
    TornDown = 4,
}
