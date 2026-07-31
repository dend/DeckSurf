using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace DeckSurf.App.Views
{
    /// <summary>
    /// One plugin lookup folder row; built-in locations cannot be removed.
    /// </summary>
    public sealed class PluginFolderRow
    {
        public string Path { get; set; } = string.Empty;

        public bool IsBuiltIn { get; set; }

        public Visibility BuiltInVisibility => IsBuiltIn ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RemoveVisibility => IsBuiltIn ? Visibility.Collapsed : Visibility.Visible;
    }

    public sealed partial class SettingsPage : Page
    {
        private readonly AppSettingsService appSettings = Ioc.Default.GetRequiredService<AppSettingsService>();
        private readonly PluginService pluginService = Ioc.Default.GetRequiredService<PluginService>();
        private readonly WindowService windowService = Ioc.Default.GetRequiredService<WindowService>();
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

            RebuildFolderRows();
        }

        public string ProfilesPath { get; } = Ioc.Default.GetRequiredService<ProfileService>().ProfilesRootPath;

        public ObservableCollection<PluginFolderRow> FolderRows { get; } = [];

        /// <summary>
        /// Gets the full app version: the version, then the build ID, which is
        /// the UTC compilation moment in yyyyMMdd.HHmm form stamped into the
        /// assembly at build time.
        /// </summary>
        public string AppVersionText { get; } = ComposeAppVersion();

        public string SdkVersionText { get; } = $"DeckSurf.SDK {FormatVersion(typeof(DeckSurf.SDK.Core.DeviceManager).Assembly.GetName().Version)}";

        private static string FormatVersion(Version? version) =>
            version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";

        private static string ComposeAppVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = FormatVersion(assembly.GetName().Version);
            var stamp = assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildTimestamp")?.Value;

            return string.IsNullOrEmpty(stamp) ? $"Version {version}" : $"Version {version}.{stamp}";
        }

        private void RebuildFolderRows()
        {
            FolderRows.Clear();

            foreach (var directory in pluginService.BuiltInDirectories)
            {
                FolderRows.Add(new PluginFolderRow { Path = Path.Combine(directory, "plugins"), IsBuiltIn = true });
            }

            foreach (var directory in appSettings.PluginDirectories)
            {
                FolderRows.Add(new PluginFolderRow { Path = directory, IsBuiltIn = false });
            }
        }

        // Show and dismiss keep IsOpen and Visibility in lockstep: a closed InfoBar
        // stays Visible at zero height and would otherwise still spend its group
        // spacing slots, doubling the header gap in the default no-error state.
        private void ShowPageError(string message)
        {
            PageInfoBar.Message = message;
            PageInfoBar.Visibility = Visibility.Visible;
            PageInfoBar.IsOpen = true;
        }

        private void PageInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            PageInfoBar.Visibility = Visibility.Collapsed;
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (initializingTheme || ThemeSelector.SelectedItem is not ComboBoxItem { Tag: string tag })
            {
                return;
            }

            try
            {
                if (Enum.TryParse<ElementTheme>(tag, out var theme))
                {
                    appSettings.ApplyTheme(theme);
                }
            }
            catch (Exception ex)
            {
                ShowPageError(ex.Message);
            }
        }

        private async void AddPluginFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker();
                picker.FileTypeFilter.Add("*");
                windowService.InitializePicker(picker);

                var folder = await picker.PickSingleFolderAsync();
                if (folder is not null && appSettings.AddPluginDirectory(folder.Path))
                {
                    RebuildFolderRows();
                    pluginService.Reload();
                }
            }
            catch (Exception ex)
            {
                ShowPageError(ex.Message);
            }
        }

        private void RemovePluginFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: PluginFolderRow { IsBuiltIn: false } row })
            {
                return;
            }

            try
            {
                appSettings.RemovePluginDirectory(row.Path);
                RebuildFolderRows();
                pluginService.Reload();
            }
            catch (Exception ex)
            {
                ShowPageError(ex.Message);
            }
        }

        private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(ProfilesPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = ProfilesPath,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                ShowPageError(ex.Message);
            }
        }
    }
}
