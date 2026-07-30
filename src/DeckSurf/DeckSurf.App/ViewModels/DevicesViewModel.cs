using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Services;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// One connected device card: enablement, active profile, and brightness.
    /// Every connected device is shown simultaneously with independent controls.
    /// </summary>
    public partial class DeviceItemViewModel : ObservableObject
    {
        private readonly DeviceService deviceService;
        private readonly Action<string?> reportStatus;
        private readonly Action<string, bool> setEnabled;
        private readonly Action<string, string> setActiveProfile;
        private readonly Action<string, string?> openEditor;
        private readonly bool applyBrightnessOnChange;
        private readonly bool applyEnabledOnChange;
        private bool applyProfileOnChange;
        private bool applyingBrightness;
        private int? pendingBrightness;

        public DeviceItemViewModel(
            DeviceService deviceService,
            DeviceSummary device,
            Action<string?> reportStatus,
            bool isEnabled,
            Action<string, bool> setEnabled,
            Action<string, string> setActiveProfile,
            Action<string, string?> openEditor)
        {
            this.deviceService = deviceService;
            this.reportStatus = reportStatus;
            this.setEnabled = setEnabled;
            this.setActiveProfile = setActiveProfile;
            this.openEditor = openEditor;
            Device = device;
            Brightness = 60;
            IsEnabled = isEnabled;

            // Only user-driven changes write to the device or settings; the
            // initial values above must not.
            applyBrightnessOnChange = true;
            applyEnabledOnChange = true;
        }

        public DeviceSummary Device { get; }

        public string Serial => Device.Serial;

        public string Name => Device.Name;

        /// <summary>
        /// Gets or sets the user's label for this device, for telling identical
        /// models apart. Null means the model name stands alone.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(HasNickname))]
        public partial string? Nickname { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Nickname) ? Name : Nickname;

        public bool HasNickname => !string.IsNullOrEmpty(Nickname);

        /// <summary>
        /// Gets or sets a value indicating whether the runtime drives this device.
        /// Turning a device off stops its profile and hides it from the editor;
        /// the connection itself stays.
        /// </summary>
        [ObservableProperty]
        public partial bool IsEnabled { get; set; }

        /// <summary>
        /// Gets the profiles that belong to this device. Selecting one makes it
        /// the active profile and the runtime brings it up immediately.
        /// </summary>
        public ObservableCollection<string> ProfileNames { get; } = [];

        public bool HasProfiles => ProfileNames.Count > 0;

        public bool HasNoProfiles => ProfileNames.Count == 0;

        [ObservableProperty]
        public partial string? SelectedProfileName { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotRunning))]
        public partial bool IsRunning { get; set; }

        public bool IsNotRunning => !IsRunning;

        partial void OnIsEnabledChanged(bool value)
        {
            if (applyEnabledOnChange)
            {
                setEnabled(Serial, value);
            }
        }

        partial void OnSelectedProfileNameChanged(string? value)
        {
            if (applyProfileOnChange && value is not null)
            {
                setActiveProfile(Serial, value);
            }
        }

        /// <summary>
        /// Replaces the profile list and selection from stored state, without
        /// re-announcing the selection as a user choice.
        /// </summary>
        public void SetProfiles(IReadOnlyList<string> names, string? active)
        {
            applyProfileOnChange = false;

            if (!names.SequenceEqual(ProfileNames, StringComparer.OrdinalIgnoreCase))
            {
                ProfileNames.Clear();
                foreach (var name in names)
                {
                    ProfileNames.Add(name);
                }

                OnPropertyChanged(nameof(HasProfiles));
                OnPropertyChanged(nameof(HasNoProfiles));
            }

            // Selection must be the exact list instance or the ComboBox rejects it.
            SelectedProfileName = ProfileNames.FirstOrDefault(n => string.Equals(n, active, StringComparison.OrdinalIgnoreCase));
            applyProfileOnChange = true;
        }

        /// <summary>
        /// Recomputes the running indicator from the runtime's session snapshot.
        /// </summary>
        public void UpdateStatus(IReadOnlyList<(string Serial, string ProfileName, string DeviceName)> sessions)
        {
            IsRunning = IsEnabled && sessions.Any(s => string.Equals(s.Serial, Serial, StringComparison.OrdinalIgnoreCase));
        }

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
        private void OpenEditor() => openEditor(Serial, SelectedProfileName);

        [RelayCommand]
        private void CopySerial()
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(Serial);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
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
        private readonly AppSettingsService appSettings;
        private readonly RuntimeService runtimeService;
        private readonly ProfileService profileService;
        private readonly WindowService windowService;

        public DevicesViewModel(
            DeviceService deviceService,
            AppSettingsService appSettings,
            RuntimeService runtimeService,
            ProfileService profileService,
            WindowService windowService)
        {
            this.deviceService = deviceService;
            this.appSettings = appSettings;
            this.runtimeService = runtimeService;
            this.profileService = profileService;
            this.windowService = windowService;
            deviceService.Devices.CollectionChanged += OnDevicesChanged;

            // Session changes carry both the running indicator and, when a
            // profile was activated elsewhere (the editor), a new selection.
            runtimeService.StateChanged += (_, _) => windowService.PostToUIThread(() =>
            {
                RefreshProfiles();
                UpdateStatuses();
            });

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

        /// <summary>
        /// Re-reads each device's profile list and active selection from storage;
        /// called when returning to the page so editor-side changes show up.
        /// </summary>
        public void RefreshProfiles()
        {
            foreach (var item in Items)
            {
                item.SetProfiles(
                    ProfilesForSerial(item.Serial),
                    appSettings.ActiveProfiles.TryGetValue(item.Serial, out var active) ? active : null);
            }
        }

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
                    Items.Add(new DeviceItemViewModel(
                        deviceService,
                        device,
                        message => StatusMessage = message,
                        appSettings.IsDeviceEnabled(device.Serial),
                        OnDeviceEnabledChanged,
                        OnActiveProfileChanged,
                        OnOpenEditor)
                    {
                        Nickname = appSettings.GetDeviceNickname(device.Serial),
                    });
                }
            }

            RefreshProfiles();
            UpdateStatuses();
            OnPropertyChanged(nameof(HasNoDevices));
            OnPropertyChanged(nameof(HasDevices));
        }

        private void OnDeviceEnabledChanged(string serial, bool enabled)
        {
            // Persisting the choice nudges the runtime to reconcile; the status
            // recomputes now for the disable case and again when sessions settle.
            appSettings.SetDeviceEnabled(serial, enabled);
            UpdateStatuses();
        }

        /// <summary>
        /// Applies a nickname (null or whitespace clears it), persists it, and
        /// refreshes the device list so the editor's combo picks up the label.
        /// </summary>
        public void SetNickname(DeviceItemViewModel item, string? nickname)
        {
            appSettings.SetDeviceNickname(item.Serial, nickname);
            item.Nickname = appSettings.GetDeviceNickname(item.Serial);
            deviceService.Refresh();
        }

        private void OnOpenEditor(string serial, string? profileName)
        {
            CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                .GetRequiredService<ProfileEditorViewModel>()
                .OpenFor(serial, profileName);
            windowService.RequestNavigation("editor");
        }

        private void OnActiveProfileChanged(string serial, string profileName)
        {
            // Off the UI thread: activating a profile opens the device and paints
            // every key, which is blocking USB work.
            _ = Task.Run(() => runtimeService.SetActiveProfile(serial, profileName));
        }

        private void UpdateStatuses()
        {
            var sessions = runtimeService.ActiveSessions;
            foreach (var item in Items)
            {
                item.UpdateStatus(sessions);
            }
        }

        private List<string> ProfilesForSerial(string serial)
        {
            var matches = new List<string>();
            foreach (var name in profileService.ListProfiles())
            {
                var profile = profileService.GetProfile(name);
                if (profile is not null && string.Equals(profile.DeviceSerial, serial, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(name);
                }
            }

            return matches;
        }
    }
}
