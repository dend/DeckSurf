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
        public SettingsPage()
        {
            InitializeComponent();
        }

        public string ProfilesPath { get; } = Ioc.Default.GetRequiredService<ProfileService>().ProfilesRootPath;

        public string ProbePathsText { get; } = string.Join(
            Environment.NewLine,
            Ioc.Default.GetRequiredService<PluginService>().ProbeDirectories.Select(d => Path.Combine(d, "plugins")));

        public string AboutText { get; } =
            $"DeckSurf {Assembly.GetExecutingAssembly().GetName().Version} · " +
            $"SDK {typeof(DeckSurf.SDK.Core.DeviceManager).Assembly.GetName().Version}";

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
