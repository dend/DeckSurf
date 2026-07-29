using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// A single entry in the runtime's button event log.
    /// </summary>
    public sealed record RuntimeLogEntry(DateTime Timestamp, string Message);

    /// <summary>
    /// Hosts the live profile runtime: owns the single open device, loads plugin
    /// command instances, dispatches button presses, and manages the
    /// activation/dispose lifecycle. This is the GUI equivalent of `deck listen`.
    /// </summary>
    public sealed class RuntimeService : IDisposable
    {
        private readonly PluginService pluginService;
        private readonly ProfileService profileService;
        private readonly object stateLock = new();

        private ConnectedDevice? device;
        private ConfigurationProfile? activeProfile;
        private Dictionary<string, IReadOnlyList<IDeckSurfCommand>> commands = [];
        private string? resumeProfileName;

        public RuntimeService(PluginService pluginService, ProfileService profileService)
        {
            this.pluginService = pluginService;
            this.profileService = profileService;

            // Auto-resume when the device for a profile stopped by disconnect reappears.
            DeviceManager.DeviceListChanged += OnDeviceListChanged;
        }

        /// <summary>
        /// Raised when the runtime starts, stops, or fails. Raised on arbitrary threads.
        /// </summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// Raised for every button-down event while running. Raised on arbitrary threads.
        /// </summary>
        public event EventHandler<RuntimeLogEntry>? ButtonEventLogged;

        public bool IsRunning { get; private set; }

        public string? ActiveProfileName { get; private set; }

        /// <summary>
        /// Gets the serial of the currently open device, when running.
        /// </summary>
        public string? ActiveDeviceSerial => device?.Serial;

        /// <summary>
        /// Starts the runtime for a stored profile. Throws when the profile or its
        /// device cannot be loaded; the caller surfaces the message in the UI.
        /// </summary>
        public void Start(string profileName)
        {
            lock (stateLock)
            {
                StopCore();

                var profile = profileService.GetProfile(profileName)
                    ?? throw new InvalidOperationException($"Profile '{profileName}' could not be loaded.");

                var openedDevice = DeviceManager.SetupDevice(profile);

                try
                {
                    openedDevice.ButtonPressed += OnButtonPressed;
                    openedDevice.DeviceDisconnected += OnDeviceDisconnected;

                    var loadedCommands = new Dictionary<string, IReadOnlyList<IDeckSurfCommand>>();
                    foreach (var plugin in pluginService.Plugins)
                    {
                        loadedCommands.Add(
                            plugin.Id.ToLowerInvariant(),
                            PluginLoader.LoadCompatibleCommands(plugin.Plugin, openedDevice.Model, message => Log(message)));
                    }

                    openedDevice.StartListening();

                    device = openedDevice;
                    activeProfile = profile;
                    commands = loadedCommands;
                    ActiveProfileName = profileName;
                    resumeProfileName = profileName;
                    IsRunning = true;

                    ActivateMappings();
                }
                catch
                {
                    openedDevice.ButtonPressed -= OnButtonPressed;
                    openedDevice.DeviceDisconnected -= OnDeviceDisconnected;
                    openedDevice.Dispose();
                    device = null;
                    activeProfile = null;
                    IsRunning = false;
                    throw;
                }
            }

            Log($"Runtime started on profile '{profileName}'.");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Stops the runtime, disposing command instances and clearing the device.
        /// Also cancels any pending auto-resume.
        /// </summary>
        public void Stop()
        {
            lock (stateLock)
            {
                resumeProfileName = null;
                if (!IsRunning)
                {
                    return;
                }

                StopCore();
            }

            Log("Runtime stopped.");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Re-applies the active profile on the already-open device: disposes the
        /// current command instances, reloads the stored profile, and re-runs
        /// activation. Used after saving profile edits while running.
        /// </summary>
        public void HotRestart()
        {
            lock (stateLock)
            {
                if (!IsRunning || device is null || ActiveProfileName is null)
                {
                    return;
                }

                DisposeCommands();

                activeProfile = profileService.GetProfile(ActiveProfileName)
                    ?? throw new InvalidOperationException($"Profile '{ActiveProfileName}' could not be reloaded.");

                var reloaded = new Dictionary<string, IReadOnlyList<IDeckSurfCommand>>();
                foreach (var plugin in pluginService.Plugins)
                {
                    reloaded.Add(
                        plugin.Id.ToLowerInvariant(),
                        PluginLoader.LoadCompatibleCommands(plugin.Plugin, device.Model, message => Log(message)));
                }

                commands = reloaded;

                device.ClearButtons();
                ActivateMappings();
            }

            Log($"Profile '{ActiveProfileName}' re-applied.");
        }

        /// <summary>
        /// Runs a short synchronous action against a device that is NOT owned by the
        /// runtime (brightness, identify). When the runtime holds the requested device,
        /// the action runs against the runtime's open instance instead of a second handle.
        /// </summary>
        public void WithDevice(string serial, Action<ConnectedDevice> action)
        {
            lock (stateLock)
            {
                if (IsRunning && device is not null && string.Equals(device.Serial, serial, StringComparison.OrdinalIgnoreCase))
                {
                    action(device);
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
            Stop();
        }

        private void StopCore()
        {
            if (device is null)
            {
                DisposeCommands();
                IsRunning = false;
                ActiveProfileName = null;
                activeProfile = null;
                return;
            }

            DisposeCommands();

            device.ButtonPressed -= OnButtonPressed;
            device.DeviceDisconnected -= OnDeviceDisconnected;

            try
            {
                device.StopListening();
                device.ClearButtons();
            }
            catch (Exception ex)
            {
                Log($"Cleanup warning: {ex.Message}");
            }

            device.Dispose();
            device = null;
            activeProfile = null;
            IsRunning = false;
            ActiveProfileName = null;
        }

        private void DisposeCommands()
        {
            foreach (var commandGroup in commands.Values)
            {
                foreach (var command in commandGroup)
                {
                    try
                    {
                        command.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log($"Command dispose warning: {ex.Message}");
                    }
                }
            }

            commands = [];
        }

        private void ActivateMappings()
        {
            if (activeProfile is null || device is null)
            {
                return;
            }

            foreach (var mapping in activeProfile.ButtonMap)
            {
                var command = FindCommand(mapping);
                if (command is not null)
                {
                    try
                    {
                        command.ExecuteOnActivation(mapping, device);
                    }
                    catch (Exception ex)
                    {
                        Log($"Activation of '{mapping.Command}' failed: {ex.Message}");
                    }
                }
            }
        }

        private IDeckSurfCommand? FindCommand(CommandMapping mapping)
        {
            if (mapping.Plugin is null || mapping.Command is null)
            {
                return null;
            }

            return commands.TryGetValue(mapping.Plugin.ToLowerInvariant(), out var pluginCommands)
                ? pluginCommands.FirstOrDefault(c => string.Equals(c.GetType().Name, mapping.Command, StringComparison.OrdinalIgnoreCase))
                : null;
        }

        private void OnButtonPressed(object? sender, ButtonPressEventArgs e)
        {
            Log($"Button {e.Id} {e.EventKind} ({e.ButtonKind}).");

            if (e.EventKind != ButtonEventKind.Down)
            {
                return;
            }

            ConfigurationProfile? profile;
            ConnectedDevice? currentDevice;
            lock (stateLock)
            {
                profile = activeProfile;
                currentDevice = device;
            }

            if (profile is null || currentDevice is null)
            {
                return;
            }

            var exactMatch = profile.ButtonMap.FirstOrDefault(m => m.ButtonIndex == e.Id);
            if (exactMatch is not null)
            {
                ExecuteAction(exactMatch, currentDevice);
            }

            foreach (var catchAll in profile.ButtonMap.Where(m => m.ButtonIndex == -1))
            {
                ExecuteAction(catchAll, currentDevice, e.Id);
            }
        }

        private void ExecuteAction(CommandMapping mapping, ConnectedDevice targetDevice, int activatingButton = -1)
        {
            var command = FindCommand(mapping);
            if (command is null)
            {
                return;
            }

            try
            {
                command.ExecuteOnAction(mapping, targetDevice, activatingButton);
            }
            catch (Exception ex)
            {
                Log($"Action '{mapping.Command}' failed: {ex.Message}");
            }
        }

        private void OnDeviceDisconnected(object? sender, EventArgs e)
        {
            var profileToResume = ActiveProfileName;

            lock (stateLock)
            {
                if (!IsRunning)
                {
                    return;
                }

                DisposeCommands();

                if (device is not null)
                {
                    device.ButtonPressed -= OnButtonPressed;
                    device.DeviceDisconnected -= OnDeviceDisconnected;
                    device.Dispose();
                    device = null;
                }

                activeProfile = null;
                IsRunning = false;
                ActiveProfileName = null;
                resumeProfileName = profileToResume;
            }

            Log("Device disconnected — runtime stopped. Will resume when it returns.");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
        {
            string? profileName;
            lock (stateLock)
            {
                if (IsRunning || resumeProfileName is null || e.Added.Count == 0)
                {
                    return;
                }

                profileName = resumeProfileName;
            }

            try
            {
                Start(profileName);
                Log($"Device reconnected — resumed profile '{profileName}'.");
            }
            catch (Exception ex)
            {
                Log($"Auto-resume failed: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            ButtonEventLogged?.Invoke(this, new RuntimeLogEntry(DateTime.Now, message));
        }
    }
}
