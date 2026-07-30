using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Helpers;
using DeckSurf.App.Services;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace DeckSurf.App.ViewModels
{
    public partial class ProfileEditorViewModel : ObservableObject
    {
        private readonly ProfileService profileService;
        private readonly PluginService pluginService;
        private readonly RuntimeService runtimeService;
        private readonly DeviceService deviceService;
        private readonly WindowService windowService;
        private readonly AppSettingsService appSettings;

        private bool loadingKey;
        private bool loadingProfile;
        private bool refreshingProfiles;
        private CancellationTokenSource? autoSaveCts;
        private CancellationTokenSource? choiceQueryCts;
        private CancellationTokenSource? choiceRefreshCts;

        public ProfileEditorViewModel(
            ProfileService profileService,
            PluginService pluginService,
            RuntimeService runtimeService,
            DeviceService deviceService,
            WindowService windowService,
            AppSettingsService appSettings)
        {
            this.profileService = profileService;
            this.pluginService = pluginService;
            this.runtimeService = runtimeService;
            this.deviceService = deviceService;
            this.windowService = windowService;
            this.appSettings = appSettings;

            pluginService.PluginsChanged += (_, _) => windowService.RunOnUIThread(() => OnPropertyChanged(nameof(Plugins)));

            // Live previews: mirror what running sessions draw on the hardware
            // onto the stage tiles of the device being edited.
            runtimeService.KeyFrameRendered += OnLiveKeyFrame;
            runtimeService.ScreenFrameRendered += OnLiveScreenFrame;
            runtimeService.StateChanged += OnRuntimeStateChanged;

            // Editing is scoped to a device; pick the first connected one. Setting
            // SelectedDevice refreshes the profile list; without devices, refresh
            // explicitly so all profiles stay reachable. Posted, never inline: this
            // handler runs before the bound Device ComboBox sees the collection
            // change, and pushing a not-yet-known SelectedItem into it throws.
            deviceService.Devices.CollectionChanged += (_, _) => windowService.PostToUIThread(RebuildDeviceList);
            appSettings.DeviceEnablementChanged += (_, _) => windowService.PostToUIThread(RebuildDeviceList);

            RebuildDeviceList();
            if (SelectedDevice is null)
            {
                RefreshProfiles();
            }
        }

        /// <summary>
        /// Points the editor at a device and, when given, one of its profiles.
        /// Used by the Devices page to jump straight into editing.
        /// </summary>
        public void OpenFor(string serial, string? profileName)
        {
            var device = ConnectedDevices.FirstOrDefault(d => string.Equals(d.Serial, serial, StringComparison.OrdinalIgnoreCase));
            if (device is not null && !ReferenceEquals(device, SelectedDevice))
            {
                SelectedDevice = device;
            }

            if (profileName is not null)
            {
                var match = ProfileNames.FirstOrDefault(p => string.Equals(p, profileName, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    SelectedProfileName = match;
                }
            }
        }

        // The editor offers connected devices that are enabled. The list re-syncs
        // on hotplug and enablement changes; when the current device leaves the
        // list, selection falls to the first remaining one.
        private void RebuildDeviceList()
        {
            for (var i = ConnectedDevices.Count - 1; i >= 0; i--)
            {
                var existing = ConnectedDevices[i];
                if (!deviceService.Devices.Contains(existing) || !appSettings.IsDeviceEnabled(existing.Serial))
                {
                    ConnectedDevices.RemoveAt(i);
                }
            }

            foreach (var device in deviceService.Devices)
            {
                if (appSettings.IsDeviceEnabled(device.Serial) && !ConnectedDevices.Contains(device))
                {
                    ConnectedDevices.Add(device);
                }
            }

            if (SelectedDevice is null || !ConnectedDevices.Contains(SelectedDevice))
            {
                SelectedDevice = ConnectedDevices.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasDevices));
        }

        public ObservableCollection<string> ProfileNames { get; } = [];

        public ObservableCollection<KeyViewModel> Keys { get; } = [];

        public ObservableCollection<KeyViewModel> CatchAllMappings { get; } = [];

        public ObservableCollection<KeyViewModel> KnobTargets { get; } = [];

        public ObservableCollection<KeyViewModel> ScreenTargets { get; } = [];

        /// <summary>
        /// Gets the touch keys flanking the screen on devices that have them
        /// (Stream Deck Neo): index 0 left of the strip, index 1 right of it.
        /// Separate single-item collections so each side renders in place.
        /// </summary>
        public ObservableCollection<KeyViewModel> TouchLeftTargets { get; } = [];

        public ObservableCollection<KeyViewModel> TouchRightTargets { get; } = [];

        public ObservableCollection<ParameterFieldViewModel> ParameterFields { get; } = [];

        public IReadOnlyList<PluginInfo> Plugins => pluginService.Plugins;

        /// <summary>
        /// Gets the devices offered for editing: connected devices the user has
        /// not disabled. Disabled devices, and with them their profiles, stay out
        /// of the editor until they are enabled again on the Devices page.
        /// </summary>
        public ObservableCollection<DeviceSummary> ConnectedDevices { get; } = [];

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

        /// <summary>
        /// Gets or sets the latest health report from the selected command's
        /// <see cref="IDeckSurfStatusProvider"/>, shown under the parameter form.
        /// Null when the command reports no status.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCommandStatus))]
        public partial string? CommandStatusText { get; set; }

        [ObservableProperty]
        public partial InfoBarSeverity CommandStatusSeverity { get; set; }

        public bool HasCommandStatus => !string.IsNullOrEmpty(CommandStatusText);

        [ObservableProperty]
        public partial int GridColumns { get; set; }

        [ObservableProperty]
        public partial bool ShowScreenStrip { get; set; }

        [ObservableProperty]
        public partial bool ShowKnobs { get; set; }

        [ObservableProperty]
        public partial bool ShowTouchKeys { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatus))]
        public partial string? StatusMessage { get; set; }

        public bool HasProfile => !string.IsNullOrEmpty(SelectedProfileName);

        public bool HasNoProfiles => ProfileNames.Count == 0;

        public bool HasProfiles => ProfileNames.Count > 0;

        public bool HasDevices => ConnectedDevices.Count > 0;

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
            { Target: MappingTarget.TouchButton } touch => $"Touch key {touch.Index + 1}",
            { Index: -1 } => "Any key",
            { } key => $"Key {key.Index}",
        };

        public string InspectorSubtitle => SelectedCommand?.DisplayName ?? "Not assigned";

        public bool HasParameters => ParameterFields.Count > 0;

        public bool HasSelectedCommand => SelectedCommand is not null;

        /// <summary>
        /// Gets a value indicating whether the image section applies: a command is
        /// chosen, the target has a visible face (knobs and touch keys do not),
        /// and the command does not render its own display.
        /// </summary>
        public bool ShowImageSection =>
            HasSelectedCommand
            && SelectedKey?.Target != MappingTarget.Knob
            && SelectedKey?.Target != MappingTarget.TouchButton
            && SelectedCommand?.HasDynamicDisplay != true;

        /// <summary>
        /// Gets a value indicating whether to explain that the command draws its
        /// own button image, so the missing image section reads as intended.
        /// </summary>
        public bool ShowDynamicImageNote =>
            HasSelectedCommand
            && SelectedKey?.Target != MappingTarget.Knob
            && SelectedKey?.Target != MappingTarget.TouchButton
            && SelectedCommand?.HasDynamicDisplay == true;

        public bool HasNoParameters => SelectedCommand is not null && ParameterFields.Count == 0;

        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

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
            // Clearing the bound list makes the ComboBox push a null selection
            // mid-refresh; suppress selection handling until the list is rebuilt.
            refreshingProfiles = true;
            try
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

                SelectedProfileName = current is not null && ProfileNames.Contains(current)
                    ? current
                    : ProfileNames.FirstOrDefault();
            }
            finally
            {
                refreshingProfiles = false;
            }

            LoadProfile(SelectedProfileName);
            ActivateSelectedProfile();
            OnPropertyChanged(nameof(HasNoProfiles));
            OnPropertyChanged(nameof(HasProfiles));
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
            if (refreshingProfiles)
            {
                return;
            }

            LoadProfile(value);
            ActivateSelectedProfile();
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

        /// <summary>
        /// The profile selected in the editor becomes the device's active profile;
        /// the runtime brings the hardware in line in the background.
        /// </summary>
        private void ActivateSelectedProfile()
        {
            var serial = SelectedDevice?.Serial;
            var profileName = SelectedProfileName;
            if (serial is null || profileName is null || !ProfileBelongsToSelectedDevice(profileName))
            {
                return;
            }

            _ = Task.Run(() => runtimeService.SetActiveProfile(serial, profileName));
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

            if (oldValue is not null)
            {
                oldValue.IsSelected = false;
            }

            if (newValue is not null)
            {
                newValue.IsSelected = true;
            }

            LoadInspectorFromKey(newValue);
            OnPropertyChanged(nameof(ShowImageSection));
            OnPropertyChanged(nameof(ShowDynamicImageNote));
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
            OnPropertyChanged(nameof(ShowImageSection));
            OnPropertyChanged(nameof(ShowDynamicImageNote));

            if (!loadingKey)
            {
                RebuildParameterFields(value, existingArguments: null);
                ApplyInspectorToKey();
            }
        }

        public void CreateProfile(string name)
        {
            var device = SelectedDevice ?? ConnectedDevices.FirstOrDefault();
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

            var deletedName = SelectedProfileName;
            profileService.DeleteProfile(deletedName);
            _ = Task.Run(() => runtimeService.NotifyProfileDeleted(deletedName));
            RefreshProfiles();
        }

        /// <summary>
        /// Swaps the full mapping (command, arguments, image) between two targets
        /// of the same kind, for drag and drop between keys or between knobs.
        /// </summary>
        /// <summary>
        /// Moves a mapping from one target to another of the same kind. The source
        /// returns to its unmapped state; whatever the destination held is replaced.
        /// Live frames are dropped on both so the running device repaints them.
        /// </summary>
        public void MoveMapping(KeyViewModel source, KeyViewModel destination)
        {
            if (ReferenceEquals(source, destination) || source.Target != destination.Target)
            {
                return;
            }

            destination.PluginId = source.PluginId;
            destination.CommandId = source.CommandId;
            destination.CommandDisplayName = source.CommandDisplayName;
            destination.CommandArguments = source.CommandArguments;
            destination.ImagePath = source.ImagePath;
            destination.LiveImage = null;

            source.Clear();
            source.LiveImage = null;

            if (ReferenceEquals(SelectedKey, source) || ReferenceEquals(SelectedKey, destination))
            {
                LoadInspectorFromKey(SelectedKey);
            }

            ScheduleAutoSave();
        }

        public void SetSelectedKeyImagePath(string path)
        {
            if (SelectedKey is not null)
            {
                SelectedKey.ImagePath = path;
                ScheduleAutoSave();
            }
        }

        [RelayCommand]
        private void ClearSelectedKeyImage()
        {
            if (SelectedKey is not null)
            {
                SelectedKey.ImagePath = null;
                ScheduleAutoSave();
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
                ScheduleAutoSave();
                return;
            }

            SelectedKey.Clear();
            ScheduleAutoSave();
            LoadInspectorFromKey(SelectedKey);
        }

        /// <summary>
        /// Persists and applies edits shortly after the last change. Debounced so
        /// typing in a parameter field saves once per pause instead of per
        /// keystroke, since every save also hot-applies to the running device.
        /// </summary>
        private void ScheduleAutoSave()
        {
            if (loadingProfile || SelectedProfileName is null)
            {
                return;
            }

            autoSaveCts?.Cancel();
            var cts = autoSaveCts = new CancellationTokenSource();
            _ = AutoSaveAfterDelayAsync(cts.Token);
        }

        private async Task AutoSaveAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(600, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (!token.IsCancellationRequested)
            {
                await SaveAsync();
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedProfileName is null)
            {
                return;
            }

            // Not rescheduled from here, or every save would beget another.
            ApplyInspectorToKey(scheduleAutoSave: false);

            var profile = new ConfigurationProfile
            {
                DeviceIndex = profileDeviceIndex,
                DeviceModel = profileDeviceModel,
                DeviceSerial = profileDeviceSerial,
            };

            // Screen tiles are saved with just an image too - a background without a
            // mapped command is still meaningful.
            foreach (var key in Keys.Concat(CatchAllMappings).Concat(KnobTargets).Concat(ScreenTargets)
                .Concat(TouchLeftTargets).Concat(TouchRightTargets)
                .Where(k => k.HasMapping || (k.Target == MappingTarget.Screen && !string.IsNullOrEmpty(k.ImagePath))))
            {
                profile.ButtonMap.Add(new CommandMapping
                {
                    ButtonIndex = key.Index,
                    Target = key.Target,
                    Plugin = key.PluginId,
                    Command = key.CommandId,
                    CommandArguments = key.CommandArguments,
                    ButtonImagePath = key.ImagePath ?? string.Empty,
                });
            }

            try
            {
                profileService.SaveProfile(SelectedProfileName, profile);

                // The runtime is always on: saving applies the changes to the
                // device running this profile immediately.
                var savedName = SelectedProfileName;
                await Task.Run(() => runtimeService.NotifyProfileSaved(savedName));

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
                TouchLeftTargets.Clear();
                TouchRightTargets.Clear();

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
                var connected = ConnectedDevices.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(profile.DeviceSerial)
                    && string.Equals(d.Serial, profile.DeviceSerial, StringComparison.OrdinalIgnoreCase));

                if (connected is null && string.IsNullOrEmpty(profile.DeviceSerial))
                {
                    connected = SelectedDevice ?? ConnectedDevices.FirstOrDefault();
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

                var (columns, rows) = connected is not null
                    ? (connected.ButtonColumns, connected.ButtonRows)
                    : DeviceLayouts.GetGrid(profile.DeviceModel);

                ShowScreenStrip = connected?.IsScreenSupported ?? DeviceLayouts.HasScreen(profile.DeviceModel);
                ShowKnobs = connected?.IsKnobSupported ?? DeviceLayouts.HasKnobs(profile.DeviceModel);
                ShowTouchKeys = (connected?.TouchButtonCount ?? (profile.DeviceModel == DeviceModel.Neo ? 2 : 0)) > 0;

                BuildKeys(columns, rows);
                BuildHardwareTargets();

                foreach (var mapping in profile.ButtonMap)
                {
                    KeyViewModel? target = mapping.Target switch
                    {
                        MappingTarget.Knob when mapping.ButtonIndex >= 0 && mapping.ButtonIndex < KnobTargets.Count => KnobTargets[mapping.ButtonIndex],
                        MappingTarget.Screen when ScreenTargets.Count > 0 => ScreenTargets[0],
                        MappingTarget.TouchButton when mapping.ButtonIndex == 0 && TouchLeftTargets.Count > 0 => TouchLeftTargets[0],
                        MappingTarget.TouchButton when mapping.ButtonIndex == 1 && TouchRightTargets.Count > 0 => TouchRightTargets[0],
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
                    target.CommandDisplayName = mapping.Plugin is null || mapping.Command is null
                        ? null
                        : pluginService.GetCommand(mapping.Plugin, mapping.Command)?.DisplayName;
                    target.CommandArguments = mapping.CommandArguments;
                    target.ImagePath = mapping.ButtonImagePath;
                }

                // The deck always shows one any-key slot; it is only saved once mapped.
                if (CatchAllMappings.Count == 0)
                {
                    CatchAllMappings.Add(new KeyViewModel(-1));
                }

                // A pending auto-save from the previous profile must not fire
                // against the newly loaded one.
                autoSaveCts?.Cancel();
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
            TouchLeftTargets.Clear();
            TouchRightTargets.Clear();

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

            if (ShowTouchKeys)
            {
                TouchLeftTargets.Add(new KeyViewModel(0, MappingTarget.TouchButton));
                TouchRightTargets.Add(new KeyViewModel(1, MappingTarget.TouchButton));
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

        private void RebuildParameterFields(CommandInfo? command, CommandArguments? existingArguments)
        {
            foreach (var field in ParameterFields)
            {
                field.PropertyChanged -= OnParameterFieldChanged;
            }

            ParameterFields.Clear();

            if (command is not null)
            {
                var existingValues = existingArguments ?? CommandArguments.Empty;

                foreach (var parameter in command.Parameters)
                {
                    existingValues.TryGetValue(parameter.Key, out var currentValue);

                    // Legacy LaunchApplication-style profiles stored a bare value with
                    // no key; surface it in the first required field.
                    if (currentValue is null
                        && parameter.Required
                        && !string.IsNullOrEmpty(existingValues.LegacyText)
                        && !existingValues.LegacyText.Contains('='))
                    {
                        currentValue = existingValues.LegacyText;
                    }

                    var field = new ParameterFieldViewModel(parameter, currentValue);
                    field.PropertyChanged += OnParameterFieldChanged;
                    ParameterFields.Add(field);
                }
            }

            QueryCommandRuntimeInfo(command);

            OnPropertyChanged(nameof(HasParameters));
            OnPropertyChanged(nameof(HasNoParameters));
        }

        private void OnParameterFieldChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParameterFieldViewModel.Value))
            {
                ApplyInspectorToKey();

                // Editing a connection-ish field (host, password) changes what the
                // choice provider would return, so re-query after the typing settles.
                // Edits to the dynamic field itself don't affect its own choices.
                if (sender is ParameterFieldViewModel { HasDynamicChoices: false })
                {
                    ScheduleDynamicChoiceRefresh();
                }
            }
        }

        /// <summary>
        /// Probes the selected command's runtime capabilities off the UI thread:
        /// health via <see cref="IDeckSurfStatusProvider"/> and per-field
        /// suggestions via <see cref="IDeckSurfChoiceProvider"/>. Failures leave
        /// the status hidden and the fields as plain text input.
        /// </summary>
        private void QueryCommandRuntimeInfo(CommandInfo? command)
        {
            CommandStatusText = null;

            var isStatusProvider = command is not null && typeof(IDeckSurfStatusProvider).IsAssignableFrom(command.CommandType);
            var isChoiceProvider = command is not null && typeof(IDeckSurfChoiceProvider).IsAssignableFrom(command.CommandType);
            var dynamicFields = isChoiceProvider
                ? ParameterFields.Where(f => f.HasDynamicChoices).ToList()
                : new List<ParameterFieldViewModel>();

            if (command is null || (!isStatusProvider && dynamicFields.Count == 0))
            {
                return;
            }

            choiceQueryCts?.Cancel();
            var cts = choiceQueryCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var token = cts.Token;

            var currentValues = CommandArguments.FromDictionary(ParameterFields
                .Where(f => !string.IsNullOrEmpty(f.Value))
                .ToDictionary(f => f.Key, f => f.Value!, StringComparer.OrdinalIgnoreCase));

            _ = Task.Run(async () =>
            {
                using var instance = (IDeckSurfCommand)Activator.CreateInstance(command.CommandType)!;

                if (instance is IDeckSurfStatusProvider statusProvider)
                {
                    try
                    {
                        var status = await statusProvider.GetStatusAsync(currentValues, token);

                        if (!token.IsCancellationRequested)
                        {
                            windowService.RunOnUIThread(() =>
                            {
                                CommandStatusText = status.Message;
                                CommandStatusSeverity = status.Kind == CommandStatusKind.Ready
                                    ? InfoBarSeverity.Success
                                    : InfoBarSeverity.Warning;
                            });
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (instance is IDeckSurfChoiceProvider choiceProvider)
                {
                    foreach (var field in dynamicFields)
                    {
                        IReadOnlyList<string> choices;

                        try
                        {
                            choices = await choiceProvider.GetChoicesAsync(field.Key, currentValues, token);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        if (!token.IsCancellationRequested && choices.Count > 0)
                        {
                            windowService.RunOnUIThread(() => field.SetDynamicChoices(choices));
                        }
                    }
                }
            }, token);
        }

        private void ScheduleDynamicChoiceRefresh()
        {
            choiceRefreshCts?.Cancel();
            var cts = choiceRefreshCts = new CancellationTokenSource();
            _ = RefreshChoicesAfterDelayAsync(cts.Token);
        }

        private async Task RefreshChoicesAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(800, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (!token.IsCancellationRequested)
            {
                QueryCommandRuntimeInfo(SelectedCommand);
            }
        }

        private void ApplyInspectorToKey(bool scheduleAutoSave = true)
        {
            if (loadingKey || SelectedKey is null)
            {
                return;
            }

            SelectedKey.PluginId = SelectedPlugin?.Id;
            SelectedKey.CommandId = SelectedCommand?.Id;
            SelectedKey.CommandDisplayName = SelectedCommand?.DisplayName;

            if (ParameterFields.Count > 0)
            {
                var values = ParameterFields
                    .Where(f => !string.IsNullOrEmpty(f.Value))
                    .ToDictionary(f => f.Key, f => f.Value!, StringComparer.OrdinalIgnoreCase);

                SelectedKey.CommandArguments = CommandArguments.FromDictionary(values);
                StatusMessage = null;
            }

            if (scheduleAutoSave)
            {
                ScheduleAutoSave();
            }
        }

        private void OnLiveKeyFrame(object? sender, LiveKeyFrame frame)
        {
            windowService.RunOnUIThread(async () =>
            {
                if (!string.Equals(SelectedDevice?.Serial, frame.Serial, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var key = Keys.FirstOrDefault(k => k.Index == frame.KeyId);
                if (key is null)
                {
                    return;
                }

                // The runtime blanks vacated keys on the hardware, and that paint
                // arrives here as a pure black frame. An unmapped tile shows the
                // standard blank cap instead of mirroring the black.
                if (!key.HasMapping)
                {
                    key.LiveImage = null;
                    return;
                }

                if (await DecodeFrameAsync(frame.Image) is { } decoded)
                {
                    key.LiveImage = decoded;
                }
            });
        }

        private void OnLiveScreenFrame(object? sender, LiveScreenFrame frame)
        {
            windowService.RunOnUIThread(async () =>
            {
                if (!string.Equals(SelectedDevice?.Serial, frame.Serial, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var screen = ScreenTargets.FirstOrDefault();
                if (screen is not null && await DecodeFrameAsync(frame.Image) is { } decoded)
                {
                    screen.LiveImage = decoded;
                }
            });
        }

        private void OnRuntimeStateChanged(object? sender, EventArgs e)
        {
            windowService.RunOnUIThread(() =>
            {
                // When the edited device's session ends, the hardware goes dark;
                // the mirrored frames go with it.
                var serial = SelectedDevice?.Serial;
                if (serial is null || runtimeService.ActiveSessions.All(s => !string.Equals(s.Serial, serial, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var key in Keys.Concat(ScreenTargets).Concat(KnobTargets).Concat(CatchAllMappings))
                    {
                        key.LiveImage = null;
                    }
                }
            });
        }

        private static async Task<ImageSource?> DecodeFrameAsync(byte[] imageBytes)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                return bitmap;
            }
            catch (Exception)
            {
                // An undecodable frame (unexpected device-specific payload)
                // leaves the previous preview in place.
                return null;
            }
        }
    }
}
