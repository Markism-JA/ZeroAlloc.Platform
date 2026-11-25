namespace Platform.Memory;

internal enum NativeMapCreateStatus : byte
{
    Success = 0,
    AlreadyExists = 1,
    SystemError = 2,
}
