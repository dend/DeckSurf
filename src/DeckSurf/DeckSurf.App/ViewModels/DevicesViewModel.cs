using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Services;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// One connected device with its own controls; every connected device is shown
    /// simultaneously with independent brightness and identify actions.
    /// </summary>
    public partial class DeviceItemViewModel : ObservableObject
    {
        private readonly DeviceService deviceService;
        private readonly Action<string?> reportStatus;
        private bool applyingBrightness;
        private int? pendingBrightness;

        public DeviceItemViewModel(DeviceService deviceService, DeviceSummary device, Action<string?> reportStatus)
        {
            this.deviceService = deviceService;
            this.reportStatus = reportStatus;
            Device = device;
            Brightness = 60;
        }

        public DeviceSummary Device { get; }

        public string Serial => Device.Serial;

        public string Name => Device.Name;

        // The full identity in one sentence; the redundant Layout row was folded in
        // here so the expander says everything once.
        public string Subtitle => $"{Device.Model}, serial {Device.Serial}, {LayoutText}";

        public string LayoutText
        {
            get
            {
                List<string> extras = [];
                if (Device.IsScreenSupported)
                {
                    extras.Add("screen");
                }

                if (Device.IsKnobSupported)
                {
                    extras.Add("knobs");
                }

                if (Device.TouchButtonCount > 0)
                {
                    extras.Add($"{Device.TouchButtonCount} touch keys");
                }

                var layout = $"{Device.ButtonColumns} x {Device.ButtonRows} keys at {Device.ButtonResolution}px";
                var extrasText = extras.Count switch
                {
                    0 => null,
                    1 => extras[0],
                    2 => $"{extras[0]} and {extras[1]}",
                    _ => string.Join(", ", extras[..^1]) + $", and {extras[^1]}",
                };

                return extrasText is null ? layout : $"{layout}, with {extrasText}";
            }
        }

        [ObservableProperty]
        public partial double Brightness { get; set; }

        public string BrightnessText => $"{(int)Brightness}%";

        partial void OnBrightnessChanged(double value)
        {
            OnPropertyChanged(nameof(BrightnessText));
        }

        [RelayCommand]
        public async Task ApplyBrightnessAsync()
        {
            // Coalesce rapid slider changes: while a write is in flight, only remember
            // the latest requested level and apply it once the current write finishes.
            pendingBrightness = (int)Brightness;
            if (applyingBrightness)
            {
                return;
            }

            applyingBrightness = true;
            try
            {
                while (pendingBrightness is int level)
                {
                    pendingBrightness = null;
                    await Task.Run(() => deviceService.SetBrightness(Serial, level));
                }

                reportStatus(null);
            }
            catch (Exception ex)
            {
                pendingBrightness = null;
                reportStatus($"{Name}: {ex.Message}");
            }
            finally
            {
                applyingBrightness = false;
            }
        }

        [RelayCommand]
        private async Task IdentifyAsync()
        {
            try
            {
                await Task.Run(() => deviceService.Identify(Serial));
                reportStatus(null);
            }
            catch (Exception ex)
            {
                reportStatus($"{Name}: {ex.Message}");
            }
        }
    }

    public partial class DevicesViewModel : ObservableObject
    {
        private readonly DeviceService deviceService;

        public DevicesViewModel(DeviceService deviceService)
        {
            this.deviceService = deviceService;
            deviceService.Devices.CollectionChanged += OnDevicesChanged;
            SyncItems();
        }

        public ObservableCollection<DeviceItemViewModel> Items { get; } = [];

        public bool HasNoDevices => Items.Count == 0;

        public bool HasDevices => Items.Count > 0;

        /// <summary>
        /// Gets the page-level failure message; per-device failures summarize into it
        /// prefixed with the device name.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatus))]
        public partial string? StatusMessage { get; set; }

        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        [RelayCommand]
        private void Refresh() => deviceService.Refresh();

        private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncItems();
        }

        // Keep existing items (and their slider state) for devices that are still
        // connected; add and remove the rest.
        private void SyncItems()
        {
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var serial = Items[i].Serial;
                if (!deviceService.Devices.Any(d => string.Equals(d.Serial, serial, StringComparison.OrdinalIgnoreCase)))
                {
                    Items.RemoveAt(i);
                }
            }

            foreach (var device in deviceService.Devices)
            {
                if (!Items.Any(i => string.Equals(i.Serial, device.Serial, StringComparison.OrdinalIgnoreCase)))
                {
                    Items.Add(new DeviceItemViewModel(deviceService, device, message => StatusMessage = message));
                }
            }

            OnPropertyChanged(nameof(HasNoDevices));
            OnPropertyChanged(nameof(HasDevices));
        }
    }
}
