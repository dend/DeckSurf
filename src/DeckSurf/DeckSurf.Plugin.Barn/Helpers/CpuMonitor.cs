using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;

namespace DeckSurf.Plugin.Barn.Helpers
{
    /// <summary>
    /// Cross-platform system-wide CPU usage measurement.
    /// </summary>
    internal static class CpuMonitor
    {
        /// <summary>
        /// Returns system-wide CPU usage as a percentage (0-100).
        /// Blocks for a short interval to sample CPU counters.
        /// Returns -1 if measurement fails on the current platform.
        /// </summary>
        internal static int GetSystemCpuUsage()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsCpuUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetLinuxCpuUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacOSCpuUsage();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPU measurement failed: {ex.Message}");
            }

            return -1;
        }

        [SupportedOSPlatform("windows")]
        private static int GetWindowsCpuUsage()
        {
            using var perfCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            // First call always returns zero — it establishes the baseline.
            perfCounter.NextValue();
            Thread.Sleep(500);
            return (int)Math.Round(perfCounter.NextValue());
        }

        private static int GetLinuxCpuUsage()
        {
            var first = ReadLinuxProcStat();
            Thread.Sleep(500);
            var second = ReadLinuxProcStat();

            long totalDelta = second.total - first.total;
            if (totalDelta == 0) return 0;

            long idleDelta = second.idle - first.idle;
            return (int)((totalDelta - idleDelta) * 100 / totalDelta);
        }

        private static (long idle, long total) ReadLinuxProcStat()
        {
            var lines = File.ReadAllLines("/proc/stat");
            var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
            if (cpuLine == null)
            {
                throw new InvalidOperationException("/proc/stat does not contain an aggregate cpu line.");
            }

            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // parts[0] = "cpu", parts[1..] = user nice system idle iowait irq softirq steal ...
            var values = parts.Skip(1).Select(long.Parse).ToArray();
            if (values.Length < 5)
            {
                throw new InvalidOperationException("/proc/stat cpu line has unexpected format.");
            }

            long idle = values[3] + values[4]; // idle + iowait
            long total = values.Sum();
            return (idle, total);
        }

        private static int GetMacOSCpuUsage()
        {
            // Run top for two samples — the first is cumulative since boot,
            // the second is the delta over the sampling interval.
            var psi = new ProcessStartInfo("top", "-l 2 -n 0 -s 1")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return -1;
            }

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            // Parse the last "CPU usage:" line for the delta sample.
            var matches = Regex.Matches(output, @"CPU usage:\s+([\d.]+)% user,\s+([\d.]+)% sys");
            if (matches.Count < 2)
            {
                return -1;
            }

            var lastMatch = matches[matches.Count - 1];
            double user = double.Parse(lastMatch.Groups[1].Value);
            double sys = double.Parse(lastMatch.Groups[2].Value);
            return (int)Math.Round(user + sys);
        }
    }
}
