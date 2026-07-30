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

        public string Note => IsBuiltIn ? "Built-in location" : "Custom folder";

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

        public string AboutText { get; } =
            $"Version {Assembly.GetExecutingAssembly().GetName().Version}, " +
            $"SDK {typeof(DeckSurf.SDK.Core.DeviceManager).Assembly.GetName().Version}";

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

        private async void AddPluginFolder_Click(object sender, RoutedEventArgs e)
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

        private void RemovePluginFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PluginFolderRow { IsBuiltIn: false } row })
            {
                appSettings.RemovePluginDirectory(row.Path);
                RebuildFolderRows();
                pluginService.Reload();
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
