using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Platform.Client.Diagnostics;

public static class ClientDiagnostics
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FailFast(ReadOnlySpan<byte> utf8Message)
    {
        string message = Encoding.UTF8.GetString(utf8Message);
        FailFast(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FailFast(string message)
    {
        Console.Error.WriteLine($"[UltraGate Client Fatal] {message}");
        Environment.FailFast(message);
    }
}
