using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly AppSettingsService appSettings = Ioc.Default.GetRequiredService<AppSettingsService>();
        private bool initializingTheme = true;

        public SettingsPage()
        {
            InitializeComponent();

            foreach (var item in ThemeSelector.Items.Cast<ComboBoxItem>())
            {
                if ((string)item.Tag == appSettings.Theme.ToString())
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }

            ThemeSelector.SelectedItem ??= ThemeSelector.Items[0];
            initializingTheme = false;
        }

        public string ProfilesPath { get; } = Ioc.Default.GetRequiredService<ProfileService>().ProfilesRootPath;

        public string ProbePathsText { get; } = string.Join(
            Environment.NewLine,
            Ioc.Default.GetRequiredService<PluginService>().ProbeDirectories.Select(d => Path.Combine(d, "plugins")));

        public string AboutText { get; } =
            $"Version {Assembly.GetExecutingAssembly().GetName().Version} · " +
            $"SDK {typeof(DeckSurf.SDK.Core.DeviceManager).Assembly.GetName().Version}";

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (initializingTheme || ThemeSelector.SelectedItem is not ComboBoxItem { Tag: string tag })
            {
                return;
            }

            if (Enum.TryParse<ElementTheme>(tag, out var theme))
            {
                appSettings.ApplyTheme(theme);
            }
        }

        private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(ProfilesPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = ProfilesPath,
                UseShellExecute = true,
            });
        }
    }
}
