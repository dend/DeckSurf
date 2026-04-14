using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace DeckSurf.Plugin.Barn.Helpers
{
    internal static class NetworkMonitor
    {
        private static long _lastBytesSent;
        private static long _lastBytesReceived;
        private static DateTime _lastSampleTime;
        private static readonly object _lock = new();

        internal static (long bytesSentPerSec, long bytesReceivedPerSec) GetThroughput()
        {
            lock (_lock)
            {
                try
                {
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up
                                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                    long totalSent = 0;
                    long totalReceived = 0;

                    foreach (var iface in interfaces)
                    {
                        try
                        {
                            var stats = iface.GetIPStatistics();
                            totalSent += stats.BytesSent;
                            totalReceived += stats.BytesReceived;
                        }
                        catch
                        {
                            // Some virtual interfaces (Docker, etc.) may throw.
                        }
                    }

                    var now = DateTime.UtcNow;

                    if (_lastSampleTime == default)
                    {
                        _lastBytesSent = totalSent;
                        _lastBytesReceived = totalReceived;
                        _lastSampleTime = now;
                        return (0, 0);
                    }

                    double elapsed = (now - _lastSampleTime).TotalSeconds;
                    if (elapsed <= 0) return (0, 0);

                    long deltaSent = Math.Max(0, totalSent - _lastBytesSent);
                    long deltaReceived = Math.Max(0, totalReceived - _lastBytesReceived);

                    _lastBytesSent = totalSent;
                    _lastBytesReceived = totalReceived;
                    _lastSampleTime = now;

                    return ((long)(deltaSent / elapsed), (long)(deltaReceived / elapsed));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Network measurement failed: {ex.Message}");
                    return (-1, -1);
                }
            }
        }

        internal static string FormatBytes(long bytesPerSec)
        {
            if (bytesPerSec < 0) return "N/A";
            if (bytesPerSec < 1024) return $"{bytesPerSec} B";
            if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:0.#} K";
            if (bytesPerSec < 1024L * 1024 * 1024) return $"{bytesPerSec / (1024.0 * 1024):0.#} M";
            return $"{bytesPerSec / (1024.0 * 1024 * 1024):0.#} G";
        }
    }
}
