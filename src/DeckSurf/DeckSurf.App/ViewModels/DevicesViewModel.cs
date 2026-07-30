using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Services;
using Microsoft.UI.Xaml;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// One row of the miniature deck rendering on a device card.
    /// </summary>
    public sealed record KeyRowViewModel(IReadOnlyList<KeyCellViewModel> Cells);

    /// <summary>
    /// One key cap in the miniature deck rendering; carries its own geometry so
    /// the cell DataTemplate binds within its own DataType scope.
    /// </summary>
    public sealed record KeyCellViewModel(double Size, CornerRadius Corner);

    /// <summary>
    /// One connected device with its own controls; every connected device is shown
    /// simultaneously with independent brightness and identify actions.
    /// </summary>
    public partial class DeviceItemViewModel : ObservableObject
    {
        private readonly DeviceService deviceService;
        private readonly Action<string?> reportStatus;
        private readonly bool applyBrightnessOnChange;
        private bool applyingBrightness;
        private int? pendingBrightness;

        public DeviceItemViewModel(DeviceService deviceService, DeviceSummary device, Action<string?> reportStatus)
        {
            this.deviceService = deviceService;
            this.reportStatus = reportStatus;
            Device = device;
            Brightness = 60;

            // Hero geometry: the miniature's pitch is derived from the hero region
            // (content box ~152px tall, ~500px wide) so the rendering commands the
            // region on every model, capped at 64 so small devices read as
            // small-but-close rather than inflated.
            var reserved = (HasScreen ? 22 : 0) + (HasKnobs ? 26 : 0);
            var pitch = Math.Min(64, Math.Min((152 - reserved) / device.ButtonRows, 500 / device.ButtonColumns));
            var cellSize = pitch - 4;
            var corner = new CornerRadius(cellSize >= 40 ? 8 : 4);
            MiniGridWidth = (device.ButtonColumns * pitch) - 4;
            ScreenStripWidth = HasTouchKeys ? MiniGridWidth - 44 : MiniGridWidth;

            var cells = Enumerable.Range(0, device.ButtonColumns)
                .Select(_ => new KeyCellViewModel(cellSize, corner))
                .ToList();
            KeyRows = [.. Enumerable.Range(0, device.ButtonRows).Select(_ => new KeyRowViewModel(cells))];
            KnobCells = [.. Enumerable.Range(0, device.KnobCount)];

            // Only user-driven slider changes write to the device; the initial
            // value above must not.
            applyBrightnessOnChange = true;
        }

        public DeviceSummary Device { get; }

        public string Serial => Device.Serial;

        public string Name => Device.Name;

        /// <summary>
        /// Gets the miniature key grid rows. Per-cell view models exist so the cell
        /// DataTemplate's x:Bind scopes to its own DataType; per-device sizing
        /// cannot compile from an Int32-typed template.
        /// </summary>
        public IReadOnlyList<KeyRowViewModel> KeyRows { get; }

        public IReadOnlyList<int> KnobCells { get; }

        public double MiniGridWidth { get; }

        /// <summary>
        /// Gets the screen strip width. On touch-key devices the strip is flanked
        /// by two 14px touch caps with 8px gaps, so the full row spans exactly
        /// <see cref="MiniGridWidth"/> and no element protrudes past the key grid.
        /// </summary>
        public double ScreenStripWidth { get; }

        public bool HasScreen => Device.IsScreenSupported;

        public bool HasKnobs => Device.KnobCount > 0;

        public bool HasTouchKeys => Device.TouchButtonCount > 0;

        public string KeyCountText => (Device.ButtonColumns * Device.ButtonRows).ToString();

        public string KnobCountText => Device.KnobCount.ToString();

        public string TouchKeyCountText => Device.TouchButtonCount.ToString();

        [ObservableProperty]
        public partial double Brightness { get; set; }

        public string BrightnessText => $"{(int)Brightness}%";

        partial void OnBrightnessChanged(double value)
        {
            OnPropertyChanged(nameof(BrightnessText));

            if (applyBrightnessOnChange)
            {
                _ = ApplyBrightnessAsync();
            }
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
