using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DeckSurf.Plugin.Barn.Helpers
{
    /// <summary>
    /// One machine-wide metrics loop shared by every dynamic-display command
    /// instance. A single timer samples CPU, RAM, and network throughput and
    /// keeps one history series per metric, so every button on every connected
    /// device shows the same numbers at the same moment. Per-instance timers
    /// would split the delta-based counters between callers, leaving each
    /// button with a different slice of the traffic.
    /// </summary>
    internal static class SystemSampler
    {
        private const int MaxHistory = 30;
        private const int IntervalMilliseconds = 1000;

        private static readonly object Gate = new();
        private static readonly List<int> CpuSeries = new();
        private static readonly List<int> RamSeries = new();
        private static readonly List<long> NetSeries = new();

        private static System.Timers.Timer _timer;
        private static EventHandler _handlers;
        private static int _cpu = -1;
        private static int _ram = -1;
        private static long _up = -1;
        private static long _down = -1;

        /// <summary>
        /// Raised on a timer thread after every sampling tick. The timer runs
        /// while at least one handler is attached.
        /// </summary>
        internal static event EventHandler SampleAvailable
        {
            add
            {
                lock (Gate)
                {
                    _handlers += value;

                    if (_timer == null)
                    {
                        _timer = new System.Timers.Timer(IntervalMilliseconds);
                        _timer.Elapsed += (s, e) => Sample();
                        _timer.Start();
                    }
                }
            }

            remove
            {
                lock (Gate)
                {
                    _handlers -= value;

                    if (_handlers == null && _timer != null)
                    {
                        _timer.Stop();
                        _timer.Dispose();
                        _timer = null;
                    }
                }
            }
        }

        internal static (int Value, List<int> Series) GetCpu()
        {
            lock (Gate)
            {
                return (_cpu, new List<int>(CpuSeries));
            }
        }

        internal static (int Value, List<int> Series) GetRam()
        {
            lock (Gate)
            {
                return (_ram, new List<int>(RamSeries));
            }
        }

        internal static (long Up, long Down, List<long> Series) GetNetwork()
        {
            lock (Gate)
            {
                return (_up, _down, new List<long>(NetSeries));
            }
        }

        private static void Sample()
        {
            try
            {
                // Measure outside the gate; the monitors take their own locks and
                // the CPU baseline bootstrap can sleep briefly on the first call.
                int cpu = CpuMonitor.GetSystemCpuUsage();
                int ram = MemoryMonitor.GetSystemMemoryUsagePercent();
                var (up, down) = NetworkMonitor.GetThroughput();

                EventHandler handlers;
                lock (Gate)
                {
                    if (cpu >= 0)
                    {
                        _cpu = cpu;
                        Push(CpuSeries, cpu);
                    }

                    if (ram >= 0)
                    {
                        _ram = ram;
                        Push(RamSeries, ram);
                    }

                    if (up >= 0)
                    {
                        _up = up;
                        _down = down;
                        Push(NetSeries, up + down);
                    }

                    handlers = _handlers;
                }

                handlers?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"System sampling tick failed: {ex}");
            }
        }

        private static void Push<T>(List<T> series, T value)
        {
            series.Add(value);
            if (series.Count > MaxHistory)
            {
                series.RemoveAt(0);
            }
        }
    }
}
