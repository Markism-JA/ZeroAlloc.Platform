using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Platform.Benchmarking.Harness.Environment;

public static partial class ProcessPriorityGuard
{
    public static bool TryElevate()
    {
        try
        {
            Process current = Process.GetCurrentProcess();

            try
            {
                current.PriorityClass = ProcessPriorityClass.RealTime;
                return true;
            }
            catch
            {
                current.PriorityClass = ProcessPriorityClass.High;
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Inspects the host OS power scheme/governor and issues warnings if frequency scaling
    /// or power saving modes will contaminate TSC/timer measurements.
    /// </summary>
    public static void WarnIfThrottled()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                InspectLinuxGovernor();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InspectWindowsPowerScheme();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                InspectMacOsThrottling();
            }
        }
        catch
        {
            // Non-critical diagnostic inspection should never throw in a benchmark runner
        }
    }

    private static void InspectLinuxGovernor()
    {
        const string governorPath = "/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor";
        if (!File.Exists(governorPath))
            return;

        string governor = File.ReadAllText(governorPath).Trim();
        if (!string.Equals(governor, "performance", StringComparison.OrdinalIgnoreCase))
        {
            EmitWarning(
                $"Linux CPU scaling governor is set to '{governor}'. "
                    + "For deterministic results, run: 'echo performance | sudo tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor'"
            );
        }
    }

    private static void InspectWindowsPowerScheme()
    {
        // GUID_MIN_POWER_SAVINGS = 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c (High Performance)
        // Ultimate Performance  = e9a42b02-d5df-448d-aa00-03f14749eb61
        Guid highPerfGuid = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        Guid ultimatePerfGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

        nint pActiveGuid = 0;
        try
        {
            uint status = Powrprof.PowerGetActiveScheme(0, out pActiveGuid);
            if (status == 0 && pActiveGuid != 0)
            {
                Guid activeGuid = Marshal.PtrToStructure<Guid>(pActiveGuid);

                if (activeGuid != highPerfGuid && activeGuid != ultimatePerfGuid)
                {
                    EmitWarning(
                        $"Windows power scheme is set to Balanced/Power Saver ({activeGuid}). "
                            + "For deterministic low-latency benchmarks, enable 'High Performance' or 'Ultimate Performance' via powercfg."
                    );
                }
            }
        }
        finally
        {
            if (pActiveGuid != 0)
            {
                Kernel32.LocalFree(pActiveGuid);
            }
        }
    }

    private static void InspectMacOsThrottling()
    {
        // macOS does not expose user governors, but we can verify low-power mode via pmset
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/pmset",
                Arguments = "-g",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        if (process.Start())
        {
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(500);

            if (output.Contains("lowpowermode 1", StringComparison.OrdinalIgnoreCase))
            {
                EmitWarning(
                    "macOS Low Power Mode is currently ACTIVE. CPU clock cycles and IPC throughput will be throttled."
                );
            }
        }
    }

    private static void EmitWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ResetColor();
    }
}
