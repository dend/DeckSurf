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
            pluginService.PluginsChanged += (_, _) => windowService.RunOnUIThread(() => OnPropertyChanged(nameof(Plugins)));

            // Editing is scoped to a device; pick the first connected one. Setting
            // SelectedDevice refreshes the profile list; without devices, refresh
            // explicitly so all profiles stay reachable.
            deviceService.Devices.CollectionChanged += (_, _) => windowService.RunOnUIThread(() =>
            {
                if (SelectedDevice is null || !deviceService.Devices.Contains(SelectedDevice))
                {
                    SelectedDevice = deviceService.Devices.FirstOrDefault();
                }
            });

            SelectedDevice = deviceService.Devices.FirstOrDefault();
            if (SelectedDevice is null)
            {
                RefreshProfiles();
            }
        }

        public ObservableCollection<string> ProfileNames { get; } = [];

        public ObservableCollection<KeyViewModel> Keys { get; } = [];

        public ObservableCollection<KeyViewModel> CatchAllMappings { get; } = [];

        public ObservableCollection<KeyViewModel> KnobTargets { get; } = [];

        public ObservableCollection<KeyViewModel> ScreenTargets { get; } = [];

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
        [NotifyPropertyChangedFor(nameof(InspectorTitle))]
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

        public bool HasNoProfiles => ProfileNames.Count == 0;

        public bool HasSelectedKey => SelectedKey is not null;

        public bool HasNoSelectedKey => SelectedKey is null;

        /// <summary>
        /// Gets the contextual header for the configuration pane, naming the selected target.
        /// </summary>
        public string InspectorTitle => SelectedKey switch
        {
            null => "Key configuration",
            { Target: MappingTarget.Knob } knob => $"Knob {knob.Index + 1}",
            { Target: MappingTarget.Screen } => "Touch screen",
            { Index: -1 } => "Any key",
            { } key => $"Key {key.Index}",
        };

        public string InspectorSubtitle => SelectedCommand?.DisplayName ?? "Not assigned";

        public bool HasParameters => ParameterFields.Count > 0;

        public bool HasSelectedCommand => SelectedCommand is not null;

        public bool HasNoParameters => SelectedCommand is not null && ParameterFields.Count == 0;

        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        public bool HasNoRuntimeLog => RuntimeLog.Count == 0;

        /// <summary>
        /// Gets the selected plugin's commands that are compatible with the profile's
        /// device. Commands without compatibility annotations work everywhere.
        /// </summary>
        public IReadOnlyList<CommandInfo> AvailableCommands
        {
            get
            {
                if (SelectedPlugin is null)
                {
                    return [];
                }

                var model = SelectedDevice?.Model ?? profileDeviceModel;
                return [.. SelectedPlugin.Commands.Where(c => c.CompatibleModels.Count == 0 || c.CompatibleModels.Contains(model))];
            }
        }

        private DeviceModel profileDeviceModel = DeviceModel.XL;
        private string? profileDeviceSerial;
        private int profileDeviceIndex;

        /// <summary>
        /// Rebuilds the profile list scoped to the selected device. Profiles belong to
        /// a device; a profile written for one layout is never silently retargeted to
        /// another. Profiles without a stored serial (legacy CLI profiles) appear for
        /// every device and adopt the device they are saved under.
        /// </summary>
        public void RefreshProfiles()
        {
            var current = SelectedProfileName;
            ProfileNames.Clear();
            foreach (var name in profileService.ListProfiles())
            {
                if (ProfileBelongsToSelectedDevice(name))
                {
                    ProfileNames.Add(name);
                }
            }

            if (current is not null && ProfileNames.Contains(current))
            {
                SelectedProfileName = current;
            }
            else
            {
                SelectedProfileName = ProfileNames.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasNoProfiles));
        }

        private bool ProfileBelongsToSelectedDevice(string name)
        {
            if (SelectedDevice is null)
            {
                // No devices connected: show everything so profiles stay editable.
                return true;
            }

            try
            {
                var profile = profileService.GetProfile(name);
                return profile is null
                    || string.IsNullOrEmpty(profile.DeviceSerial)
                    || string.Equals(profile.DeviceSerial, SelectedDevice.Serial, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        partial void OnSelectedProfileNameChanged(string? value)
        {
            LoadProfile(value);
        }

        partial void OnSelectedDeviceChanged(DeviceSummary? value)
        {
            OnPropertyChanged(nameof(AvailableCommands));

            // Switching the device switches the editing scope: the profile list
            // reloads for that device. The loaded profile is never retargeted.
            if (!loadingProfile)
            {
                RefreshProfiles();
            }
        }

        partial void OnSelectedKeyChanged(KeyViewModel? oldValue, KeyViewModel? newValue)
        {
            // One any-key slot is always present; surplus entries that were abandoned
            // without a command assignment are dropped when the selection moves on.
            if (oldValue is { Index: -1 } previous
                && !previous.HasMapping
                && CatchAllMappings.Count > 1
                && CatchAllMappings.Contains(previous))
            {
                CatchAllMappings.Remove(previous);
            }

            LoadInspectorFromKey(newValue);
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
            OnPropertyChanged(nameof(InspectorSubtitle));
            OnPropertyChanged(nameof(HasSelectedCommand));

            if (!loadingKey)
            {
                RebuildParameterFields(value, existingArguments: null);
                ApplyInspectorToKey();
            }
        }

        public void CreateProfile(string name)
        {
            var device = SelectedDevice ?? deviceService.Devices.FirstOrDefault();
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

            // Surplus any-key entries are removed outright; the last slot is only
            // cleared so the deck always shows one.
            if (SelectedKey.Index == -1 && CatchAllMappings.Count > 1)
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

            // Screen tiles are saved with just an image too — a background without a
            // mapped command is still meaningful.
            foreach (var key in Keys.Concat(CatchAllMappings).Concat(KnobTargets).Concat(ScreenTargets)
                .Where(k => k.HasMapping || (k.Target == MappingTarget.Screen && !string.IsNullOrEmpty(k.ImagePath))))
            {
                profile.ButtonMap.Add(new CommandMapping
                {
                    ButtonIndex = key.Index,
                    Target = key.Target,
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

        private KeyViewModel AddCatchAllTile()
        {
            var tile = new KeyViewModel(-1);
            CatchAllMappings.Add(tile);
            return tile;
        }

        private void LoadProfile(string? name)
        {
            loadingProfile = true;
            try
            {
                SelectedKey = null;
                Keys.Clear();
                CatchAllMappings.Clear();
                KnobTargets.Clear();
                ScreenTargets.Clear();
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

                // Prefer the profile's own device when connected; legacy profiles
                // without a stored serial belong to whichever device is being edited.
                var connected = deviceService.Devices.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(profile.DeviceSerial)
                    && string.Equals(d.Serial, profile.DeviceSerial, StringComparison.OrdinalIgnoreCase));

                if (connected is null && string.IsNullOrEmpty(profile.DeviceSerial))
                {
                    connected = SelectedDevice ?? deviceService.Devices.FirstOrDefault();
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
                BuildHardwareTargets();

                foreach (var mapping in profile.ButtonMap)
                {
                    KeyViewModel? target = mapping.Target switch
                    {
                        MappingTarget.Knob when mapping.ButtonIndex >= 0 && mapping.ButtonIndex < KnobTargets.Count => KnobTargets[mapping.ButtonIndex],
                        MappingTarget.Screen when ScreenTargets.Count > 0 => ScreenTargets[0],
                        MappingTarget.Key when mapping.ButtonIndex >= 0 && mapping.ButtonIndex < Keys.Count => Keys[mapping.ButtonIndex],
                        MappingTarget.Key when mapping.ButtonIndex == -1 => AddCatchAllTile(),
                        _ => null,
                    };

                    if (target is null)
                    {
                        continue;
                    }

                    target.PluginId = mapping.Plugin;
                    target.CommandId = mapping.Command;
                    target.CommandArguments = mapping.CommandArguments;
                    target.ImagePath = mapping.ButtonImagePath;
                }

                // The deck always shows one any-key slot; it is only saved once mapped.
                if (CatchAllMappings.Count == 0)
                {
                    CatchAllMappings.Add(new KeyViewModel(-1));
                }

                dirty = false;
            }
            finally
            {
                loadingProfile = false;
            }
        }

        private const int PlusKnobCount = 4;

        private void BuildKeys(int columns, int rows)
        {
            Keys.Clear();
            GridColumns = columns;

            for (var i = 0; i < columns * rows; i++)
            {
                Keys.Add(new KeyViewModel(i));
            }
        }

        private void BuildHardwareTargets()
        {
            KnobTargets.Clear();
            ScreenTargets.Clear();

            if (ShowKnobs)
            {
                for (var i = 0; i < PlusKnobCount; i++)
                {
                    KnobTargets.Add(new KeyViewModel(i, MappingTarget.Knob));
                }
            }

            if (ShowScreenStrip)
            {
                ScreenTargets.Add(new KeyViewModel(0, MappingTarget.Screen));
            }
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
