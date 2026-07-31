using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// A live frame written to a key on a running device, mirrored for on-screen
    /// previews. Image bytes are as passed to the device, before device-specific
    /// encoding, so they decode with standard imaging APIs.
    /// </summary>
    public sealed record LiveKeyFrame(string Serial, int KeyId, byte[] Image);

    /// <summary>
    /// A live frame written to the screen of a running device.
    /// </summary>
    public sealed record LiveScreenFrame(string Serial, byte[] Image);

    /// <summary>
    /// Hosts the always-on profile runtime: while a device is connected, its active
    /// profile runs on it automatically. One session per connected device, each
    /// owning its open device handle, plugin command instances, and dispatch;
    /// sessions reconcile on hotplug, profile save, and active-profile changes.
    /// </summary>
    public sealed class RuntimeService : IDisposable
    {
        private readonly PluginService pluginService;
        private readonly ProfileService profileService;
        private readonly AppSettingsService appSettings;
        private readonly object stateLock = new();
        private readonly Dictionary<string, Session> sessions = new(StringComparer.OrdinalIgnoreCase);

        private bool disposed;

        public RuntimeService(PluginService pluginService, ProfileService profileService, AppSettingsService appSettings)
        {
            this.pluginService = pluginService;
            this.profileService = profileService;
            this.appSettings = appSettings;

            DeviceManager.DeviceListChanged += OnDeviceListChanged;
            pluginService.PluginsChanged += (_, _) => Task.Run(RestartAllSessions);
            appSettings.DeviceEnablementChanged += (_, _) => Task.Run(Sync);
        }

        /// <summary>
        /// Raised when sessions start or stop. Raised on arbitrary threads.
        /// </summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// Raised whenever a running session writes an image to a key, so the
        /// editor can mirror the hardware live. Raised on arbitrary threads.
        /// </summary>
        public event EventHandler<LiveKeyFrame>? KeyFrameRendered;

        /// <summary>
        /// Raised whenever a running session writes an image to a device screen.
        /// Raised on arbitrary threads.
        /// </summary>
        public event EventHandler<LiveScreenFrame>? ScreenFrameRendered;

        public int ActiveSessionCount
        {
            get
            {
                lock (stateLock)
                {
                    return sessions.Count;
                }
            }
        }

        /// <summary>
        /// Gets a snapshot of the running sessions as (serial, profile, device name).
        /// </summary>
        public IReadOnlyList<(string Serial, string ProfileName, string DeviceName)> ActiveSessions
        {
            get
            {
                lock (stateLock)
                {
                    return [.. sessions.Select(s => (s.Key, s.Value.ProfileName, s.Value.Device.Name))];
                }
            }
        }

        /// <summary>
        /// Records the active profile for a device and brings its session in line.
        /// The profile selected in the editor becomes the device's active profile.
        /// </summary>
        public void SetActiveProfile(string serial, string profileName)
        {
            appSettings.SetActiveProfile(serial, profileName);
            Sync();
        }

        /// <summary>
        /// Re-applies a saved profile to any session currently running it.
        /// </summary>
        public void NotifyProfileSaved(string profileName)
        {
            lock (stateLock)
            {
                var session = sessions.Values.FirstOrDefault(s => string.Equals(s.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
                if (session is not null)
                {
                    var profile = profileService.GetProfile(profileName);

                    // The profile may now target a different device; reconcile
                    // instead of hot-reloading onto the wrong hardware.
                    if (profile is not null && string.Equals(profile.DeviceSerial, session.Device.Serial, StringComparison.OrdinalIgnoreCase))
                    {
                        HotReload(session, profile);
                        return;
                    }
                }
            }

            Sync();
        }

        /// <summary>
        /// Drops a deleted profile from every device that had it active and stops
        /// its sessions.
        /// </summary>
        public void NotifyProfileDeleted(string profileName)
        {
            foreach (var serial in appSettings.ActiveProfiles
                .Where(p => string.Equals(p.Value, profileName, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Key)
                .ToList())
            {
                appSettings.SetActiveProfile(serial, null);
            }

            Sync();
        }

        /// <summary>
        /// Reconciles running sessions with connected devices and their active
        /// profiles: connected device with an active profile gets a session,
        /// everything else stops. A device with exactly one profile adopts it as
        /// active automatically.
        /// </summary>
        public void Sync()
        {
            var changed = false;

            lock (stateLock)
            {
                if (disposed)
                {
                    return;
                }

                List<(string Serial, string Name)> connected;
                try
                {
                    connected = [.. DeviceManager.GetDeviceList().Select(d => (d.Serial, d.Name))];
                }
                catch (Exception ex)
                {
                    Log(null, $"Device enumeration failed: {ex.Message}");
                    return;
                }

                var desired = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (serial, _) in connected)
                {
                    if (string.IsNullOrEmpty(serial))
                    {
                        continue;
                    }

                    // A disabled device stays connected but runs nothing.
                    if (!appSettings.IsDeviceEnabled(serial))
                    {
                        continue;
                    }

                    if (!appSettings.ActiveProfiles.TryGetValue(serial, out var profileName))
                    {
                        // No explicit choice yet: a device with exactly one matching
                        // profile adopts it, so plugging in just works.
                        var candidates = ProfilesForSerial(serial);
                        if (candidates.Count != 1)
                        {
                            continue;
                        }

                        profileName = candidates[0];
                        appSettings.SetActiveProfile(serial, profileName);
                    }

                    if (profileService.GetProfile(profileName) is not null)
                    {
                        desired[serial] = profileName;
                    }
                }

                foreach (var serial in sessions.Keys.ToList())
                {
                    if (!desired.TryGetValue(serial, out var wantedProfile)
                        || !string.Equals(sessions[serial].ProfileName, wantedProfile, StringComparison.OrdinalIgnoreCase))
                    {
                        StopSession(serial);
                        changed = true;
                    }
                }

                foreach (var (serial, profileName) in desired)
                {
                    if (!sessions.ContainsKey(serial))
                    {
                        changed |= TryStartSession(serial, profileName);
                    }
                }
            }

            if (changed)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Runs a short synchronous action against a device. When a session owns the
        /// requested device, the action runs against the session's open instance
        /// instead of a second handle.
        /// </summary>
        public void WithDevice(string serial, Action<ConnectedDevice> action)
        {
            lock (stateLock)
            {
                if (sessions.TryGetValue(serial, out var session))
                {
                    action(session.Device);
                    return;
                }
            }

            if (!DeviceManager.TryGetDeviceBySerial(serial, out var transientDevice))
            {
                throw new InvalidOperationException($"Device with serial '{serial}' is not connected.");
            }

            using (transientDevice)
            {
                action(transientDevice);
            }
        }

        public void Dispose()
        {
            DeviceManager.DeviceListChanged -= OnDeviceListChanged;

            lock (stateLock)
            {
                disposed = true;
                foreach (var serial in sessions.Keys.ToList())
                {
                    StopSession(serial);
                }
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

        private bool TryStartSession(string serial, string profileName)
        {
            var profile = profileService.GetProfile(profileName);
            if (profile is null)
            {
                return false;
            }

            ConnectedDevice openedDevice;
            try
            {
                openedDevice = DeviceManager.SetupDevice(profile)
                    ?? throw new InvalidOperationException("Device could not be opened.");
            }
            catch (Exception ex)
            {
                Log(null, $"Could not open device for profile '{profileName}': {ex.Message}");
                return false;
            }

            var session = new Session(serial, profileName, profile, openedDevice);

            try
            {
                session.ButtonHandler = (_, e) => OnButtonPressed(session, e);
                session.DisconnectHandler = (_, _) => OnSessionDeviceDisconnected(session);
                session.KeyImageHandler = (_, e) => KeyFrameRendered?.Invoke(this, new LiveKeyFrame(session.Serial, e.KeyId, e.Image));
                session.ScreenImageHandler = (_, e) => ScreenFrameRendered?.Invoke(this, new LiveScreenFrame(session.Serial, e.Image));
                openedDevice.ButtonPressed += session.ButtonHandler;
                openedDevice.DeviceDisconnected += session.DisconnectHandler;
                openedDevice.KeyImageSet += session.KeyImageHandler;
                openedDevice.ScreenImageSet += session.ScreenImageHandler;

                session.Commands = LoadCommands(openedDevice, session);

                openedDevice.StartListening();
                sessions[serial] = session;
                ActivateMappings(session);
            }
            catch (Exception ex)
            {
                openedDevice.ButtonPressed -= session.ButtonHandler;
                openedDevice.DeviceDisconnected -= session.DisconnectHandler;
                openedDevice.KeyImageSet -= session.KeyImageHandler;
                openedDevice.ScreenImageSet -= session.ScreenImageHandler;
                openedDevice.Dispose();
                Log(null, $"Could not start profile '{profileName}': {ex.Message}");
                return false;
            }

            Log(session, "Profile activated.");
            return true;
        }

        private void StopSession(string serial)
        {
            if (!sessions.TryGetValue(serial, out var session))
            {
                return;
            }

            sessions.Remove(serial);
            DisposeCommands(session);

            session.Device.ButtonPressed -= session.ButtonHandler;
            session.Device.DeviceDisconnected -= session.DisconnectHandler;
            session.Device.KeyImageSet -= session.KeyImageHandler;
            session.Device.ScreenImageSet -= session.ScreenImageHandler;

            try
            {
                session.Device.StopListening();
                session.Device.ClearButtons();

                for (var i = 0; i < session.Device.TouchButtonCount; i++)
                {
                    SetTouchKeyLight(session, i, lit: false);
                }
            }
            catch (Exception ex)
            {
                Log(session, $"Cleanup warning: {ex.Message}");
            }

            session.Device.Dispose();
            Log(session, "Profile deactivated.");
        }

        private void HotReload(Session session, ConfigurationProfile profile)
        {
            // Incremental apply: editing one key must not blink the whole board.
            // Mappings that did not change keep their live command instances and
            // key faces; only the command types behind added, changed, or removed
            // mappings are recycled, and only affected keys are blanked.
            var oldMappings = BySlot(session.Profile.ButtonMap);
            var newMappings = BySlot(profile.ButtonMap);

            var affected = new List<CommandMapping>();
            var typesToRecycle = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (slot, oldMapping) in oldMappings)
            {
                if (!newMappings.TryGetValue(slot, out var newMapping))
                {
                    // Removed: blank the key it owned.
                    BlankKey(session, oldMapping);
                    NoteAffected(typesToRecycle, oldMapping);
                }
                else if (!MappingEquals(oldMapping, newMapping))
                {
                    BlankKey(session, oldMapping);
                    NoteAffected(typesToRecycle, oldMapping);
                    NoteAffected(typesToRecycle, newMapping);
                    affected.Add(newMapping);
                }
            }

            foreach (var (slot, newMapping) in newMappings)
            {
                if (!oldMappings.ContainsKey(slot))
                {
                    NoteAffected(typesToRecycle, newMapping);
                    affected.Add(newMapping);
                }
            }

            session.Profile = profile;

            // Command-less mappings (a touch key that only carries a backlight
            // color, a screen background) still need their re-activation pass, so
            // only a fully unchanged profile short-circuits.
            if (typesToRecycle.Count == 0 && affected.Count == 0)
            {
                Log(session, "Profile changes applied.");
                return;
            }

            if (typesToRecycle.Count > 0)
            {
                RecycleCommands(session, typesToRecycle);

                // A recycled instance may also serve mappings that did not change;
                // those must re-activate on the fresh instance or their keys go dead.
                foreach (var mapping in profile.ButtonMap)
                {
                    if (mapping.Command is not null
                        && typesToRecycle.Contains(mapping.Command)
                        && !affected.Contains(mapping))
                    {
                        affected.Add(mapping);
                    }
                }
            }

            foreach (var mapping in affected)
            {
                if (mapping.Target == MappingTarget.Screen)
                {
                    RenderScreenImage(session, mapping);
                }

                if (mapping.Target == MappingTarget.TouchButton)
                {
                    LightTouchKey(session, mapping);
                }

                var command = FindCommand(session, mapping);
                if (command is not null)
                {
                    try
                    {
                        command.ExecuteOnActivation(mapping, session.Device);
                    }
                    catch (Exception ex)
                    {
                        Log(session, $"Activation of '{mapping.Command}' failed: {ex.Message}");
                    }
                }
            }

            Log(session, "Profile changes applied.");
        }

        /// <summary>
        /// Lights a mapped touch key: the mapping's declared color when it has
        /// one, otherwise a white glow so a live target is visible. Commands may
        /// still override from their own activation.
        /// </summary>
        private static void LightTouchKey(Session session, CommandMapping mapping)
        {
            if (mapping.ButtonIndex < 0 || mapping.ButtonIndex >= session.Device.TouchButtonCount)
            {
                return;
            }

            try
            {
                session.Device.SetKeyColor(
                    session.Device.ButtonCount + mapping.ButtonIndex,
                    mapping.ButtonColor ?? DeviceColor.White);
            }
            catch (Exception ex)
            {
                Log(session, $"Touch key light warning: {ex.Message}");
            }
        }

        // Slots are occurrence-indexed because a profile can hold several
        // mappings for the same target and index (the any-key catch-alls).
        private static Dictionary<(MappingTarget Target, int Index, int Occurrence), CommandMapping> BySlot(IEnumerable<CommandMapping> mappings)
        {
            var occurrences = new Dictionary<(MappingTarget, int), int>();
            var result = new Dictionary<(MappingTarget, int, int), CommandMapping>();
            foreach (var mapping in mappings)
            {
                var slot = (mapping.Target, mapping.ButtonIndex);
                occurrences[slot] = occurrences.TryGetValue(slot, out var count) ? count + 1 : 0;
                result[(mapping.Target, mapping.ButtonIndex, occurrences[slot])] = mapping;
            }

            return result;
        }

        private static bool MappingEquals(CommandMapping a, CommandMapping b) =>
            string.Equals(a.Plugin, b.Plugin, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Command, b.Command, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.ButtonImagePath ?? string.Empty, b.ButtonImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && Nullable.Equals(a.ButtonColor, b.ButtonColor)
            && ArgumentsEqual(a.CommandArguments, b.CommandArguments);

        private static bool ArgumentsEqual(CommandArguments a, CommandArguments b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var (key, value) in a)
            {
                if (!b.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void NoteAffected(HashSet<string> types, CommandMapping mapping)
        {
            if (mapping.Command is not null)
            {
                types.Add(mapping.Command);
            }
        }

        private void BlankKey(Session session, CommandMapping mapping)
        {
            if (mapping.Target == MappingTarget.TouchButton)
            {
                SetTouchKeyLight(session, mapping.ButtonIndex, lit: false);
                return;
            }

            if (mapping.Target != MappingTarget.Key || mapping.ButtonIndex < 0)
            {
                return;
            }

            try
            {
                session.Device.SetKey(
                    mapping.ButtonIndex,
                    ImageHelper.CreateBlankImage(session.Device.ButtonResolution, DeviceColor.Black));
            }
            catch (Exception ex)
            {
                Log(session, $"Key clear warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Drives a touch key's backlight (Neo): mapped keys glow so a live
        /// target is visible, unmapped keys stay dark. Commands may override the
        /// color from their own activation.
        /// </summary>
        private static void SetTouchKeyLight(Session session, int touchIndex, bool lit)
        {
            if (touchIndex < 0 || touchIndex >= session.Device.TouchButtonCount)
            {
                return;
            }

            try
            {
                session.Device.SetKeyColor(
                    session.Device.ButtonCount + touchIndex,
                    lit ? DeviceColor.White : DeviceColor.Black);
            }
            catch (Exception ex)
            {
                Log(session, $"Touch key light warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes and recreates only the command instances whose types are
        /// affected by a profile edit; every other instance keeps running.
        /// </summary>
        private void RecycleCommands(Session session, HashSet<string> commandTypes)
        {
            var rebuilt = new Dictionary<string, IReadOnlyList<IDeckSurfCommand>>();

            foreach (var (pluginId, instances) in session.Commands)
            {
                var plugin = pluginService.Plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
                var kept = new List<IDeckSurfCommand>();

                foreach (var instance in instances)
                {
                    if (commandTypes.Contains(instance.GetType().Name))
                    {
                        try
                        {
                            instance.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log(session, $"Command dispose warning: {ex.Message}");
                        }

                        var freshType = plugin is null
                            ? null
                            : PluginLoader.GetCommandTypes(plugin.Plugin, session.Device.Model)
                                .FirstOrDefault(t => t == instance.GetType());
                        if (freshType is not null)
                        {
                            try
                            {
                                kept.Add((IDeckSurfCommand)Activator.CreateInstance(freshType)!);
                            }
                            catch (Exception ex)
                            {
                                Log(session, $"Command recreate warning: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        kept.Add(instance);
                    }
                }

                rebuilt[pluginId] = kept;
            }

            session.Commands = rebuilt;
        }

        private void RestartAllSessions()
        {
            lock (stateLock)
            {
                foreach (var session in sessions.Values.ToList())
                {
                    var profile = profileService.GetProfile(session.ProfileName);
                    if (profile is not null)
                    {
                        HotReload(session, profile);
                    }
                }
            }
        }

        private Dictionary<string, IReadOnlyList<IDeckSurfCommand>> LoadCommands(ConnectedDevice device, Session session)
        {
            var loaded = new Dictionary<string, IReadOnlyList<IDeckSurfCommand>>();
            foreach (var plugin in pluginService.Plugins)
            {
                loaded.Add(
                    plugin.Id.ToLowerInvariant(),
                    PluginLoader.LoadCompatibleCommands(plugin.Plugin, device.Model, message => Log(session, message)));
            }

            return loaded;
        }

        private void DisposeCommands(Session session)
        {
            foreach (var commandGroup in session.Commands.Values)
            {
                foreach (var command in commandGroup)
                {
                    try
                    {
                        command.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log(session, $"Command dispose warning: {ex.Message}");
                    }
                }
            }

            session.Commands = [];
        }

        private void ActivateMappings(Session session)
        {
            foreach (var mapping in session.Profile.ButtonMap)
            {
                if (mapping.Target == MappingTarget.Screen)
                {
                    RenderScreenImage(session, mapping);
                }

                if (mapping.Target == MappingTarget.TouchButton)
                {
                    LightTouchKey(session, mapping);
                }

                var command = FindCommand(session, mapping);
                if (command is not null)
                {
                    try
                    {
                        command.ExecuteOnActivation(mapping, session.Device);
                    }
                    catch (Exception ex)
                    {
                        Log(session, $"Activation of '{mapping.Command}' failed: {ex.Message}");
                    }
                }
            }
        }

        private void RenderScreenImage(Session session, CommandMapping mapping)
        {
            if (!session.Device.IsScreenSupported
                || string.IsNullOrEmpty(mapping.ButtonImagePath)
                || !File.Exists(mapping.ButtonImagePath))
            {
                return;
            }

            try
            {
                var resized = ImageHelper.ResizeImage(
                    File.ReadAllBytes(mapping.ButtonImagePath),
                    session.Device.ScreenWidth,
                    session.Device.ScreenHeight,
                    DeviceRotation.None,
                    DeviceImageFormat.Jpeg);
                session.Device.SetScreen(resized, 0, session.Device.ScreenWidth, session.Device.ScreenHeight);
            }
            catch (Exception ex)
            {
                Log(session, $"Screen image render failed: {ex.Message}");
            }
        }

        private IDeckSurfCommand? FindCommand(Session session, CommandMapping mapping)
        {
            if (mapping.Plugin is null || mapping.Command is null)
            {
                return null;
            }

            return session.Commands.TryGetValue(mapping.Plugin.ToLowerInvariant(), out var pluginCommands)
                ? pluginCommands.FirstOrDefault(c => string.Equals(c.GetType().Name, mapping.Command, StringComparison.OrdinalIgnoreCase))
                : null;
        }

        private void OnButtonPressed(Session session, ButtonPressEventArgs e)
        {
            Log(session, $"Button {e.Id} {e.EventKind} ({e.ButtonKind}).");

            switch (e.ButtonKind)
            {
                case ButtonKind.Knob:
                    foreach (var knobMapping in session.Profile.ButtonMap.Where(m => m.Target == MappingTarget.Knob && m.ButtonIndex == e.Id))
                    {
                        ExecuteEvent(session, knobMapping, e);
                    }

                    break;
                case ButtonKind.Screen:
                    foreach (var screenMapping in session.Profile.ButtonMap.Where(m => m.Target == MappingTarget.Screen))
                    {
                        ExecuteEvent(session, screenMapping, e);
                    }

                    break;
                default:
                    if (e.EventKind != ButtonEventKind.Down)
                    {
                        return;
                    }

                    // Touch keys (Neo) report as plain buttons past the key grid;
                    // they have their own mapping target and skip the catch-alls,
                    // which are a key-grid concept.
                    if (session.Device.TouchButtonCount > 0 && e.Id >= session.Device.ButtonCount)
                    {
                        var touchIndex = e.Id - session.Device.ButtonCount;
                        foreach (var touchMapping in session.Profile.ButtonMap.Where(m => m.Target == MappingTarget.TouchButton && m.ButtonIndex == touchIndex))
                        {
                            ExecuteAction(session, touchMapping);
                        }

                        return;
                    }

                    var exactMatch = session.Profile.ButtonMap.FirstOrDefault(m => m.Target == MappingTarget.Key && m.ButtonIndex == e.Id);
                    if (exactMatch is not null)
                    {
                        ExecuteAction(session, exactMatch);
                    }

                    foreach (var catchAll in session.Profile.ButtonMap.Where(m => m.Target == MappingTarget.Key && m.ButtonIndex == -1))
                    {
                        ExecuteAction(session, catchAll, e.Id);
                    }

                    break;
            }
        }

        private void ExecuteEvent(Session session, CommandMapping mapping, ButtonPressEventArgs e)
        {
            var command = FindCommand(session, mapping);
            if (command is null)
            {
                return;
            }

            try
            {
                command.ExecuteOnEvent(mapping, session.Device, e);
            }
            catch (Exception ex)
            {
                Log(session, $"Event handler '{mapping.Command}' failed: {ex.Message}");
            }
        }

        private void ExecuteAction(Session session, CommandMapping mapping, int activatingButton = -1)
        {
            var command = FindCommand(session, mapping);
            if (command is null)
            {
                return;
            }

            try
            {
                command.ExecuteOnAction(mapping, session.Device, activatingButton);
            }
            catch (Exception ex)
            {
                Log(session, $"Action '{mapping.Command}' failed: {ex.Message}");
            }
        }

        private void OnSessionDeviceDisconnected(Session session)
        {
            var changed = false;

            lock (stateLock)
            {
                if (sessions.TryGetValue(session.Serial, out var current) && ReferenceEquals(current, session))
                {
                    StopSession(session.Serial);
                    changed = true;
                }
            }

            if (changed)
            {
                Log(null, $"{session.Device.Name} disconnected. Its profile resumes when it returns.");
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
        {
            Sync();
        }

        private static void Log(Session? session, string message)
        {
            var source = session is null ? "runtime" : $"{session.ProfileName} on {session.Device.Name}";
            System.Diagnostics.Debug.WriteLine($"[{source}] {message}");
        }

        private sealed class Session(string serial, string profileName, ConfigurationProfile profile, ConnectedDevice device)
        {
            public string Serial { get; } = serial;

            public string ProfileName { get; } = profileName;

            public ConfigurationProfile Profile { get; set; } = profile;

            public ConnectedDevice Device { get; } = device;

            public Dictionary<string, IReadOnlyList<IDeckSurfCommand>> Commands { get; set; } = [];

            public EventHandler<ButtonPressEventArgs>? ButtonHandler { get; set; }

            public EventHandler<EventArgs>? DisconnectHandler { get; set; }

            public EventHandler<KeyImageSetEventArgs>? KeyImageHandler { get; set; }

            public EventHandler<ScreenImageSetEventArgs>? ScreenImageHandler { get; set; }
        }
    }
}
