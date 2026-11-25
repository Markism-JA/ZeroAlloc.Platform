namespace Platform.Memory;

internal enum NativeMapOpenStatus : byte
{
    Success = 0,
    NotFound = 1,
    NotInitialized = 2,
    AccessDenied = 3,
    InvalidSize = 4,
    SystemError = 5,
}
