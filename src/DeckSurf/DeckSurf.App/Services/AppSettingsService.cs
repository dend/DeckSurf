using System.Text.Json;
using Microsoft.UI.Xaml;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Persists small app-level preferences (currently the theme) to a JSON file
    /// alongside the DeckSurf profile storage.
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

        private void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var settings = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(SettingsPath));
                    if (settings is not null && Enum.TryParse<ElementTheme>(settings.Theme, out var theme))
                    {
                        Theme = theme;
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
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new PersistedSettings { Theme = Theme.ToString() }));
            }
            catch (Exception)
            {
                // Failure to persist preferences is non-fatal.
            }
        }

        private sealed class PersistedSettings
        {
            public string? Theme { get; set; }
        }
    }
}
