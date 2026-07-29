using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Services;
using DeckSurf.SDK.Models;

namespace DeckSurf.App.ViewModels
{
    public partial class DevicesViewModel : ObservableObject
    {
        private readonly DeviceService deviceService;
        private bool applyingBrightness;
        private int? pendingBrightness;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        public partial DeviceSummary? SelectedDevice { get; set; }

        [ObservableProperty]
        public partial double Brightness { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatus))]
        public partial string? StatusMessage { get; set; }

        public DevicesViewModel(DeviceService deviceService)
        {
            this.deviceService = deviceService;
            Brightness = 60;
            Devices.CollectionChanged += OnDevicesChanged;
        }

        public ObservableCollection<DeviceSummary> Devices => deviceService.Devices;

        public bool HasNoDevices => Devices.Count == 0;

        public bool HasSelection => SelectedDevice is not null;

        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        public static string FormatIdentity(DeviceModel model, string serial)
        {
            return $"{model} · {serial}";
        }

        public static string FormatModel(DeviceSummary? device)
        {
            return device?.Model.ToString() ?? string.Empty;
        }

        public static string FormatBrightness(double brightness)
        {
            return $"{(int)brightness}%";
        }

        public static string FormatLayout(DeviceSummary? device)
        {
            return device is null ? string.Empty : $"{device.ButtonColumns} x {device.ButtonRows} keys @ {device.ButtonResolution}px";
        }

        public static string FormatExtras(DeviceSummary? device)
        {
            if (device is null)
            {
                return string.Empty;
            }

            List<string> extras = [];
            if (device.IsScreenSupported)
            {
                extras.Add("screen");
            }

            if (device.IsKnobSupported)
            {
                extras.Add("knobs");
            }

            if (device.TouchButtonCount > 0)
            {
                extras.Add($"{device.TouchButtonCount} touch keys");
            }

            return extras.Count == 0 ? "none" : string.Join(", ", extras);
        }

        [RelayCommand]
        private void Refresh() => deviceService.Refresh();

        [RelayCommand]
        private async Task IdentifyAsync()
        {
            var serial = SelectedDevice?.Serial;
            if (serial is null)
            {
                return;
            }

            try
            {
                await Task.Run(() => deviceService.Identify(serial));
                StatusMessage = null;
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task ApplyBrightnessAsync()
        {
            var serial = SelectedDevice?.Serial;
            if (serial is null)
            {
                return;
            }

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
                    await Task.Run(() => deviceService.SetBrightness(serial, level));
                }

                StatusMessage = null;
            }
            catch (Exception ex)
            {
                pendingBrightness = null;
                StatusMessage = ex.Message;
            }
            finally
            {
                applyingBrightness = false;
            }
        }

        private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasNoDevices));
            if (SelectedDevice is not null && !Devices.Contains(SelectedDevice))
            {
                SelectedDevice = null;
            }
        }
    }
}
