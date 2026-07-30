using System.Collections.ObjectModel;
using DeckSurf.SDK.Core;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using Microsoft.UI.Dispatching;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Snapshot of a connected device's identity and capabilities.
    /// </summary>
    public sealed record DeviceSummary(
        string Name,
        string Serial,
        DeviceModel Model,
        int ButtonCount,
        int ButtonColumns,
        int ButtonRows,
        int ButtonResolution,
        bool IsScreenSupported,
        bool IsKnobSupported,
        int KnobCount,
        int TouchButtonCount)
    {
        public string DisplayText => $"{Name} ({Serial})";
    }

    /// <summary>
    /// Enumerates connected Stream Deck devices and tracks hotplug changes,
    /// marshaling updates onto the UI thread.
    /// </summary>
    public sealed class DeviceService : IDisposable
    {
        private readonly RuntimeService runtimeService;
        private DispatcherQueue? dispatcherQueue;

        public DeviceService(RuntimeService runtimeService)
        {
            this.runtimeService = runtimeService;
        }

        /// <summary>
        /// Gets the connected devices. Only mutated on the UI thread after <see cref="Initialize"/>.
        /// </summary>
        public ObservableCollection<DeviceSummary> Devices { get; } = [];

        public void Initialize(DispatcherQueue queue)
        {
            dispatcherQueue = queue;
            DeviceManager.DeviceListChanged += OnDeviceListChanged;
            Refresh();
        }

        public void Refresh()
        {
            List<DeviceSummary> summaries = [];
            try
            {
                foreach (var device in DeviceManager.GetDeviceList())
                {
                    summaries.Add(new DeviceSummary(
                        device.Name,
                        device.Serial,
                        device.Model,
                        device.ButtonCount,
                        device.ButtonColumns,
                        device.ButtonRows,
                        device.ButtonResolution,
                        device.IsScreenSupported,
                        device.IsKnobSupported,
                        device.KnobCount,
                        device.TouchButtonCount));
                }
            }
            catch (Exception)
            {
                // Enumeration failures (e.g., HID access contention) leave the last
                // known list in place.
                return;
            }

            RunOnUIThread(() =>
            {
                Devices.Clear();
                foreach (var summary in summaries)
                {
                    Devices.Add(summary);
                }
            });
        }

        /// <summary>
        /// Sets device brightness (0-100). Synchronous USB I/O - call off the UI thread.
        /// </summary>
        public void SetBrightness(string serial, int level)
        {
            runtimeService.WithDevice(serial, device => device.SetBrightness((byte)Math.Clamp(level, 0, 100)));
        }

        /// <summary>
        /// Briefly flashes all keys white so the user can tell devices apart.
        /// Synchronous USB I/O - call off the UI thread.
        /// </summary>
        public void Identify(string serial)
        {
            runtimeService.WithDevice(serial, device =>
            {
                var white = ImageHelper.CreateBlankImage(device.ButtonResolution, DeviceColor.White);
                for (var pass = 0; pass < 2; pass++)
                {
                    for (var i = 0; i < device.ButtonCount; i++)
                    {
                        device.SetKey(i, white, alreadyResized: true);
                    }

                    Thread.Sleep(150);
                    device.ClearButtons();
                    Thread.Sleep(150);
                }
            });
        }

        public void Dispose()
        {
            DeviceManager.DeviceListChanged -= OnDeviceListChanged;
        }

        private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
        {
            Refresh();
        }

        private void RunOnUIThread(Action action)
        {
            var queue = dispatcherQueue;
            if (queue is null || queue.HasThreadAccess)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(() => action());
            }
        }
    }
}
