using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Persists small app-level preferences (theme, custom plugin folders) to a JSON
    /// file alongside the DeckSurf profile storage.
    /// </summary>
    public sealed class AppSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Den.Dev", "DeckSurf", "app-settings.json");

        private readonly WindowService windowService;

        public AppSettingsService(WindowService windowService)
        {
            this.windowService = windowService;
            Load();
        }

        /// <summary>
        /// Raised when a device is enabled or disabled. Raised on the caller's thread.
        /// </summary>
        public event EventHandler? DeviceEnablementChanged;

        public ElementTheme Theme { get; private set; } = ElementTheme.Default;

        /// <summary>
        /// Gets the user-configured plugin lookup folders, scanned in addition to the
        /// built-in locations next to the application.
        /// </summary>
        public ObservableCollection<string> PluginDirectories { get; } = [];

        /// <summary>
        /// Gets the active profile per device serial. While a device is connected,
        /// its active profile runs on it automatically.
        /// </summary>
        public Dictionary<string, string> ActiveProfiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the serials of devices the user has turned off. A disabled device
        /// keeps its connection but the runtime leaves it alone: no profile runs
        /// on it and the editor does not offer it.
        /// </summary>
        public HashSet<string> DisabledDevices { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsDeviceEnabled(string serial) =>
            !string.IsNullOrWhiteSpace(serial) && !DisabledDevices.Contains(serial);

        /// <summary>
        /// Gets user-given device nicknames per serial, for telling identical
        /// models apart.
        /// </summary>
        public Dictionary<string, string> DeviceNicknames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetDeviceNickname(string serial) =>
            !string.IsNullOrWhiteSpace(serial) && DeviceNicknames.TryGetValue(serial, out var nickname) ? nickname : null;

        /// <summary>
        /// Sets or clears (null or whitespace) a device's nickname and persists it.
        /// </summary>
        public void SetDeviceNickname(string serial, string? nickname)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                return;
            }

            var trimmed = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();

            if (trimmed is null)
            {
                if (!DeviceNicknames.Remove(serial))
                {
                    return;
                }
            }
            else
            {
                if (string.Equals(GetDeviceNickname(serial), trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                DeviceNicknames[serial] = trimmed;
            }

            Save();
        }

        /// <summary>
        /// Enables or disables a device and persists the choice.
        /// </summary>
        public void SetDeviceEnabled(string serial, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                return;
            }

            var changed = enabled ? DisabledDevices.Remove(serial) : DisabledDevices.Add(serial);
            if (changed)
            {
                Save();
                DeviceEnablementChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Records the active profile for a device and persists the choice.
        /// Passing null clears the entry.
        /// </summary>
        public void SetActiveProfile(string serial, string? profileName)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                return;
            }

            if (profileName is null)
            {
                if (!ActiveProfiles.Remove(serial))
                {
                    return;
                }
            }
            else
            {
                if (ActiveProfiles.TryGetValue(serial, out var current)
                    && string.Equals(current, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                ActiveProfiles[serial] = profileName;
            }

            Save();
        }

        public void ApplyTheme(ElementTheme theme)
        {
            Theme = theme;

            if (windowService.MainWindow?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }

            Save();
        }

        /// <summary>
        /// Applies the persisted theme to the current window; call once after the
        /// main window content exists.
        /// </summary>
        public void ApplySavedTheme()
        {
            if (Theme != ElementTheme.Default && windowService.MainWindow?.Content is FrameworkElement root)
            {
                root.RequestedTheme = Theme;
            }
        }

        /// <summary>
        /// Adds a plugin lookup folder. Returns false when it is already configured.
        /// </summary>
        public bool AddPluginDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || PluginDirectories.Any(d => string.Equals(d, path, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            PluginDirectories.Add(path);
            Save();
            return true;
        }

        public void RemovePluginDirectory(string path)
        {
            var existing = PluginDirectories.FirstOrDefault(d => string.Equals(d, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                PluginDirectories.Remove(existing);
                Save();
            }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var settings = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(SettingsPath));
                    if (settings is null)
                    {
                        return;
                    }

                    if (Enum.TryParse<ElementTheme>(settings.Theme, out var theme))
                    {
                        Theme = theme;
                    }

                    foreach (var directory in settings.PluginDirectories ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            PluginDirectories.Add(directory);
                        }
                    }

                    foreach (var (serial, profile) in settings.ActiveProfiles ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(serial) && !string.IsNullOrWhiteSpace(profile))
                        {
                            ActiveProfiles[serial] = profile;
                        }
                    }

                    foreach (var serial in settings.DisabledDevices ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(serial))
                        {
                            DisabledDevices.Add(serial);
                        }
                    }

                    foreach (var (serial, nickname) in settings.DeviceNicknames ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(serial) && !string.IsNullOrWhiteSpace(nickname))
                        {
                            DeviceNicknames[serial] = nickname;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt or unreadable settings fall back to defaults.
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new PersistedSettings
                {
                    Theme = Theme.ToString(),
                    PluginDirectories = [.. PluginDirectories],
                    ActiveProfiles = new Dictionary<string, string>(ActiveProfiles),
                    DisabledDevices = [.. DisabledDevices],
                    DeviceNicknames = new Dictionary<string, string>(DeviceNicknames),
                }));
            }
            catch (Exception)
            {
                // Failure to persist preferences is non-fatal.
            }
        }

        private sealed class PersistedSettings
        {
            public string? Theme { get; set; }

            public List<string>? PluginDirectories { get; set; }

            public Dictionary<string, string>? ActiveProfiles { get; set; }

            public List<string>? DisabledDevices { get; set; }

            public Dictionary<string, string>? DeviceNicknames { get; set; }
        }
    }
}
