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

        public ElementTheme Theme { get; private set; } = ElementTheme.Default;

        /// <summary>
        /// Gets the user-configured plugin lookup folders, scanned in addition to the
        /// built-in locations next to the application.
        /// </summary>
        public ObservableCollection<string> PluginDirectories { get; } = [];

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
        }
    }
}
