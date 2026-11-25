using System.Runtime.CompilerServices;

namespace Platform.Client.Threading;

public readonly unsafe struct ThreadParkerHandle(
    int* isSleepingState,
    delegate* <void*, void> unparkCallback,
    void* context
)
{
    public const int StateRunning = 0;
    public const int StateParked = 1;
    public const int StateSignaled = 2;

    public int* SleepingStatePointer => isSleepingState;
    public bool IsAllocated => unparkCallback != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WakeIfSleeping() => Unpark();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark()
    {
        if (unparkCallback == null)
        {
            return;
        }

        if (isSleepingState != null)
        {
            int prevState = Interlocked.Exchange(ref *isSleepingState, StateSignaled);
            if (prevState != StateParked)
            {
                return;
            }
        }

        unparkCallback(context);
    }
}
