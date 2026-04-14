using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DeckSurf.Plugin.Barn.Helpers
{
    internal static class MemoryMonitor
    {
        internal static int GetSystemMemoryUsagePercent()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsMemoryUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetLinuxMemoryUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacOSMemoryUsage();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Memory measurement failed: {ex.Message}");
            }

            return -1;
        }

        private static int GetWindowsMemoryUsage()
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref memStatus))
                return -1;

            return (int)memStatus.dwMemoryLoad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static int GetLinuxMemoryUsage()
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            long total = ParseMemInfoLine(lines.First(l => l.StartsWith("MemTotal:")));
            long available = ParseMemInfoLine(lines.First(l => l.StartsWith("MemAvailable:")));

            if (total == 0) return 0;
            return (int)((total - available) * 100 / total);
        }

        private static long ParseMemInfoLine(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return long.Parse(parts[1]);
        }

        private static int GetMacOSMemoryUsage()
        {
            long pageSize = RunSysctl("hw.pagesize");
            long totalMem = RunSysctl("hw.memsize");

            var psi = new ProcessStartInfo("vm_stat")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return -1;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            long free = ParseVmStatLine(output, "Pages free") * pageSize;
            long inactive = ParseVmStatLine(output, "Pages inactive") * pageSize;
            long speculative = ParseVmStatLine(output, "Pages speculative") * pageSize;

            long available = free + inactive + speculative;
            if (totalMem == 0) return 0;
            return (int)((totalMem - available) * 100 / totalMem);
        }

        private static long RunSysctl(string key)
        {
            var psi = new ProcessStartInfo("sysctl", $"-n {key}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return 0;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return long.TryParse(output, out var val) ? val : 0;
        }

        private static long ParseVmStatLine(string output, string label)
        {
            var match = Regex.Match(output, $@"{Regex.Escape(label)}:\s+(\d+)");
            return match.Success && long.TryParse(match.Groups[1].Value, out var val) ? val : 0;
        }
    }
}
