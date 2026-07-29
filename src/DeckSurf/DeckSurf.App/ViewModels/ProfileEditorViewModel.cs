using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Helpers;
using DeckSurf.App.Services;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;

namespace DeckSurf.App.ViewModels
{
    public partial class ProfileEditorViewModel : ObservableObject
    {
        private const int MaxLogEntries = 200;

        private readonly ProfileService profileService;
        private readonly PluginService pluginService;
        private readonly RuntimeService runtimeService;
        private readonly DeviceService deviceService;
        private readonly WindowService windowService;

        private bool loadingKey;
        private bool loadingProfile;
        private bool dirty;

        public ProfileEditorViewModel(
            ProfileService profileService,
            PluginService pluginService,
            RuntimeService runtimeService,
            DeviceService deviceService,
            WindowService windowService)
        {
            this.profileService = profileService;
            this.pluginService = pluginService;
            this.runtimeService = runtimeService;
            this.deviceService = deviceService;
            this.windowService = windowService;

            runtimeService.StateChanged += OnRuntimeStateChanged;
            runtimeService.ButtonEventLogged += OnRuntimeLog;

            RefreshProfiles();
        }

        public ObservableCollection<string> ProfileNames { get; } = [];

        public ObservableCollection<KeyViewModel> Keys { get; } = [];

        public ObservableCollection<KeyViewModel> CatchAllMappings { get; } = [];

        public ObservableCollection<ParameterFieldViewModel> ParameterFields { get; } = [];

        public ObservableCollection<string> RuntimeLog { get; } = [];

        public IReadOnlyList<PluginInfo> Plugins => pluginService.Plugins;

        public ObservableCollection<DeviceSummary> ConnectedDevices => deviceService.Devices;

        [ObservableProperty]
        public partial DeviceSummary? SelectedDevice { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasProfile))]
        public partial string? SelectedProfileName { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedKey))]
        [NotifyPropertyChangedFor(nameof(HasNoSelectedKey))]
        public partial KeyViewModel? SelectedKey { get; set; }

        [ObservableProperty]
        public partial PluginInfo? SelectedPlugin { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasParameters))]
        [NotifyPropertyChangedFor(nameof(HasNoParameters))]
        public partial CommandInfo? SelectedCommand { get; set; }

        [ObservableProperty]
        public partial int GridColumns { get; set; }

        [ObservableProperty]
        public partial bool ShowScreenStrip { get; set; }

        [ObservableProperty]
        public partial bool ShowKnobs { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotRunning))]
        public partial bool IsRunning { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatus))]
        public partial string? StatusMessage { get; set; }

        public bool IsNotRunning => !IsRunning;

        public bool HasProfile => !string.IsNullOrEmpty(SelectedProfileName);

        public bool HasSelectedKey => SelectedKey is not null;

        public bool HasNoSelectedKey => SelectedKey is null;

        public bool HasParameters => ParameterFields.Count > 0;

        public bool HasNoParameters => SelectedCommand is not null && ParameterFields.Count == 0;

        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        public bool HasNoRuntimeLog => RuntimeLog.Count == 0;

        public IReadOnlyList<CommandInfo> AvailableCommands => SelectedPlugin?.Commands ?? [];

        private DeviceModel profileDeviceModel = DeviceModel.XL;
        private string? profileDeviceSerial;
        private int profileDeviceIndex;

        public void RefreshProfiles()
        {
            var current = SelectedProfileName;
            ProfileNames.Clear();
            foreach (var name in profileService.ListProfiles())
            {
                ProfileNames.Add(name);
            }

            if (current is not null && ProfileNames.Contains(current))
            {
                SelectedProfileName = current;
            }
            else
            {
                SelectedProfileName = ProfileNames.FirstOrDefault();
            }
        }

        partial void OnSelectedProfileNameChanged(string? value)
        {
            LoadProfile(value);
        }

        partial void OnSelectedDeviceChanged(DeviceSummary? value)
        {
            if (!loadingProfile && value is not null)
            {
                ApplyDeviceSelection(value);
            }
        }

        partial void OnSelectedKeyChanged(KeyViewModel? value)
        {
            LoadInspectorFromKey(value);
        }

        partial void OnSelectedPluginChanged(PluginInfo? value)
        {
            OnPropertyChanged(nameof(AvailableCommands));

            if (!loadingKey)
            {
                SelectedCommand = null;
            }
        }

        partial void OnSelectedCommandChanged(CommandInfo? value)
        {
            if (!loadingKey)
            {
                RebuildParameterFields(value, existingArguments: null);
                ApplyInspectorToKey();
            }
        }

        public void CreateProfile(string name)
        {
            var device = deviceService.Devices.FirstOrDefault();
            var profile = new ConfigurationProfile
            {
                DeviceIndex = 0,
                DeviceModel = device?.Model ?? DeviceModel.XL,
                DeviceSerial = device?.Serial,
            };

            try
            {
                profileService.SaveProfile(name, profile);
                StatusMessage = null;
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                return;
            }

            RefreshProfiles();
            SelectedProfileName = name;
        }

        public void DeleteSelectedProfile()
        {
            if (SelectedProfileName is null)
            {
                return;
            }

            if (IsRunning && string.Equals(runtimeService.ActiveProfileName, SelectedProfileName, StringComparison.OrdinalIgnoreCase))
            {
                runtimeService.Stop();
            }

            profileService.DeleteProfile(SelectedProfileName);
            RefreshProfiles();
        }

        public void SetSelectedKeyImagePath(string path)
        {
            if (SelectedKey is not null)
            {
                SelectedKey.ImagePath = path;
                dirty = true;
            }
        }

        [RelayCommand]
        private void ClearSelectedKeyImage()
        {
            if (SelectedKey is not null)
            {
                SelectedKey.ImagePath = null;
                dirty = true;
            }
        }

        [RelayCommand]
        private void ClearSelectedKey()
        {
            if (SelectedKey is null)
            {
                return;
            }

            // Clearing an any-key tile removes it entirely; an unmapped catch-all
            // has no meaning and would be dropped on save anyway.
            if (SelectedKey.Index == -1)
            {
                CatchAllMappings.Remove(SelectedKey);
                SelectedKey = null;
                dirty = true;
                return;
            }

            SelectedKey.Clear();
            dirty = true;
            LoadInspectorFromKey(SelectedKey);
        }

        [RelayCommand]
        private void AddCatchAll()
        {
            var catchAll = new KeyViewModel(-1);
            CatchAllMappings.Add(catchAll);
            SelectedKey = catchAll;
            dirty = true;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedProfileName is null)
            {
                return;
            }

            ApplyInspectorToKey();

            var profile = new ConfigurationProfile
            {
                DeviceIndex = profileDeviceIndex,
                DeviceModel = profileDeviceModel,
                DeviceSerial = profileDeviceSerial,
            };

            foreach (var key in Keys.Concat(CatchAllMappings).Where(k => k.HasMapping))
            {
                profile.ButtonMap.Add(new CommandMapping
                {
                    ButtonIndex = key.Index,
                    Plugin = key.PluginId,
                    Command = key.CommandId,
                    CommandArguments = key.CommandArguments ?? string.Empty,
                    ButtonImagePath = key.ImagePath ?? string.Empty,
                });
            }

            try
            {
                profileService.SaveProfile(SelectedProfileName, profile);
                dirty = false;

                if (IsRunning && string.Equals(runtimeService.ActiveProfileName, SelectedProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(runtimeService.ActiveDeviceSerial, profileDeviceSerial, StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(runtimeService.HotRestart);
                    }
                    else
                    {
                        // The profile now targets a different device; the open handle
                        // cannot be reused, so restart the runtime fully.
                        var profileName = SelectedProfileName;
                        await Task.Run(() =>
                        {
                            runtimeService.Stop();
                            runtimeService.Start(profileName);
                        });
                    }
                }

                StatusMessage = null;
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task ToggleRuntimeAsync()
        {
            try
            {
                if (IsRunning)
                {
                    await Task.Run(runtimeService.Stop);
                }
                else if (SelectedProfileName is not null)
                {
                    if (dirty)
                    {
                        await SaveAsync();
                    }

                    var profileName = SelectedProfileName;
                    await Task.Run(() => runtimeService.Start(profileName));
                }

                StatusMessage = null;
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void LoadProfile(string? name)
        {
            loadingProfile = true;
            try
            {
                SelectedKey = null;
                Keys.Clear();
                CatchAllMappings.Clear();
                SelectedDevice = null;

                if (name is null)
                {
                    return;
                }

                ConfigurationProfile? profile;
                try
                {
                    profile = profileService.GetProfile(name);
                }
                catch (Exception ex)
                {
                    StatusMessage = ex.Message;
                    return;
                }

                if (profile is null)
                {
                    return;
                }

                profileDeviceModel = profile.DeviceModel;
                profileDeviceSerial = profile.DeviceSerial;
                profileDeviceIndex = profile.DeviceIndex;

                // Prefer the connected device's real layout when the profile's device is
                // present; fall back to the device-index match for profiles written by the
                // CLI, which does not store model or serial.
                var connected = deviceService.Devices.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(profile.DeviceSerial)
                    && string.Equals(d.Serial, profile.DeviceSerial, StringComparison.OrdinalIgnoreCase));

                if (connected is null && profile.DeviceIndex >= 0 && profile.DeviceIndex < deviceService.Devices.Count)
                {
                    connected = deviceService.Devices[profile.DeviceIndex];
                }

                if (connected is not null)
                {
                    // Adopt the device identity so saving repairs profiles that lack it.
                    profileDeviceModel = connected.Model;
                    if (string.IsNullOrEmpty(profileDeviceSerial))
                    {
                        profileDeviceSerial = connected.Serial;
                    }
                }

                SelectedDevice = connected;

                var (columns, rows) = connected is not null
                    ? (connected.ButtonColumns, connected.ButtonRows)
                    : DeviceLayouts.GetGrid(profile.DeviceModel);

                ShowScreenStrip = connected?.IsScreenSupported ?? DeviceLayouts.HasScreen(profile.DeviceModel);
                ShowKnobs = connected?.IsKnobSupported ?? DeviceLayouts.HasKnobs(profile.DeviceModel);

                BuildKeys(columns, rows);

                foreach (var mapping in profile.ButtonMap)
                {
                    KeyViewModel target;
                    if (mapping.ButtonIndex == -1)
                    {
                        target = new KeyViewModel(-1);
                        CatchAllMappings.Add(target);
                    }
                    else if (mapping.ButtonIndex >= 0 && mapping.ButtonIndex < Keys.Count)
                    {
                        target = Keys[mapping.ButtonIndex];
                    }
                    else
                    {
                        continue;
                    }

                    target.PluginId = mapping.Plugin;
                    target.CommandId = mapping.Command;
                    target.CommandArguments = mapping.CommandArguments;
                    target.ImagePath = mapping.ButtonImagePath;
                }

                dirty = false;
            }
            finally
            {
                loadingProfile = false;
            }
        }

        private void BuildKeys(int columns, int rows)
        {
            Keys.Clear();
            GridColumns = columns;

            for (var i = 0; i < columns * rows; i++)
            {
                Keys.Add(new KeyViewModel(i));
            }
        }

        // Retarget the loaded profile to a different connected device: adopt its
        // identity and rebuild the grid to its layout, keeping in-range mappings.
        private void ApplyDeviceSelection(DeviceSummary device)
        {
            profileDeviceModel = device.Model;
            profileDeviceSerial = device.Serial;
            profileDeviceIndex = Math.Max(0, deviceService.Devices.IndexOf(device));

            ShowScreenStrip = device.IsScreenSupported;
            ShowKnobs = device.IsKnobSupported;

            var preserved = Keys
                .Where(k => k.HasMapping)
                .Select(k => (k.Index, k.PluginId, k.CommandId, k.CommandArguments, k.ImagePath))
                .ToList();

            SelectedKey = null;
            BuildKeys(device.ButtonColumns, device.ButtonRows);

            var dropped = 0;
            foreach (var mapping in preserved)
            {
                if (mapping.Index < Keys.Count)
                {
                    var key = Keys[mapping.Index];
                    key.PluginId = mapping.PluginId;
                    key.CommandId = mapping.CommandId;
                    key.CommandArguments = mapping.CommandArguments;
                    key.ImagePath = mapping.ImagePath;
                }
                else
                {
                    dropped++;
                }
            }

            StatusMessage = dropped > 0
                ? $"{dropped} mapping(s) fell outside the {device.ButtonColumns} x {device.ButtonRows} layout and were removed."
                : null;
            dirty = true;
        }

        private void LoadInspectorFromKey(KeyViewModel? key)
        {
            loadingKey = true;
            try
            {
                if (key is null)
                {
                    SelectedPlugin = null;
                    SelectedCommand = null;
                    ParameterFields.Clear();
                    OnPropertyChanged(nameof(HasParameters));
                    OnPropertyChanged(nameof(HasNoParameters));
                    return;
                }

                SelectedPlugin = key.PluginId is null ? null : pluginService.GetPlugin(key.PluginId);
                OnPropertyChanged(nameof(AvailableCommands));
                SelectedCommand = key.PluginId is null || key.CommandId is null
                    ? null
                    : pluginService.GetCommand(key.PluginId, key.CommandId);

                RebuildParameterFields(SelectedCommand, key.CommandArguments);
            }
            finally
            {
                loadingKey = false;
            }
        }

        private void RebuildParameterFields(CommandInfo? command, string? existingArguments)
        {
            foreach (var field in ParameterFields)
            {
                field.PropertyChanged -= OnParameterFieldChanged;
            }

            ParameterFields.Clear();

            if (command is not null)
            {
                var existingValues = CommandArgumentParser.Parse(existingArguments ?? string.Empty);

                foreach (var parameter in command.Parameters)
                {
                    existingValues.TryGetValue(parameter.Key, out var currentValue);

                    // Legacy LaunchApplication-style profiles stored a bare value with
                    // no key; surface it in the first required field.
                    if (currentValue is null
                        && parameter.Required
                        && !string.IsNullOrEmpty(existingArguments)
                        && !existingArguments.Contains('='))
                    {
                        currentValue = existingArguments;
                    }

                    var field = new ParameterFieldViewModel(parameter, currentValue);
                    field.PropertyChanged += OnParameterFieldChanged;
                    ParameterFields.Add(field);
                }
            }

            OnPropertyChanged(nameof(HasParameters));
            OnPropertyChanged(nameof(HasNoParameters));
        }

        private void OnParameterFieldChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParameterFieldViewModel.Value))
            {
                ApplyInspectorToKey();
            }
        }

        private void ApplyInspectorToKey()
        {
            if (loadingKey || SelectedKey is null)
            {
                return;
            }

            SelectedKey.PluginId = SelectedPlugin?.Id;
            SelectedKey.CommandId = SelectedCommand?.Id;

            if (ParameterFields.Count > 0)
            {
                var values = ParameterFields
                    .Where(f => !string.IsNullOrEmpty(f.Value))
                    .ToDictionary(f => f.Key, f => f.Value!, StringComparer.OrdinalIgnoreCase);

                try
                {
                    SelectedKey.CommandArguments = CommandArgumentParser.Format(values);
                    StatusMessage = null;
                }
                catch (ArgumentException ex)
                {
                    StatusMessage = ex.Message;
                }
            }

            dirty = true;
        }

        private void OnRuntimeStateChanged(object? sender, EventArgs e)
        {
            windowService.RunOnUIThread(() => IsRunning = runtimeService.IsRunning);
        }

        private void OnRuntimeLog(object? sender, RuntimeLogEntry entry)
        {
            windowService.RunOnUIThread(() =>
            {
                RuntimeLog.Insert(0, $"{entry.Timestamp:HH:mm:ss}  {entry.Message}");
                while (RuntimeLog.Count > MaxLogEntries)
                {
                    RuntimeLog.RemoveAt(RuntimeLog.Count - 1);
                }

                OnPropertyChanged(nameof(HasNoRuntimeLog));
            });
        }
    }
}
