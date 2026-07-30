using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeckSurf.App
{
    /// <summary>
    /// Main application window hosting the navigation shell.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Title = "DeckSurf";

            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop();
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarArea);

            var runtimeService = Ioc.Default.GetRequiredService<RuntimeService>();
            runtimeService.StateChanged += (_, _) => DispatcherQueue.TryEnqueue(() => UpdateRuntimeStatus(runtimeService));
            UpdateRuntimeStatus(runtimeService);

            RootNavigation.SelectedItem = DevicesItem;
        }

        private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            Type pageType;
            if (args.SelectedItem is NavigationViewItem { Tag: string tag })
            {
                pageType = tag switch
                {
                    "editor" => typeof(Views.ProfileEditorPage),
                    "plugins" => typeof(Views.PluginsPage),
                    "activity" => typeof(Views.ActivityPage),
                    "settings" => typeof(Views.SettingsPage),
                    _ => typeof(Views.DevicesPage),
                };
            }
            else
            {
                return;
            }

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
            }
        }

        // The runtime is automatic: while a device is connected, its active profile
        // runs on it. The dot informs; there is nothing to start or stop.
        private void UpdateRuntimeStatus(RuntimeService runtimeService)
        {
            var activeSessions = runtimeService.ActiveSessions;
            if (activeSessions.Count > 0)
            {
                RuntimeStatusDot.Fill = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                RuntimeStatusText.Text = activeSessions.Count == 1
                    ? $"Active on {activeSessions[0].DeviceName}"
                    : $"Active on {activeSessions.Count} devices";
            }
            else
            {
                RuntimeStatusDot.Fill = (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
                RuntimeStatusText.Text = "Idle";
            }
        }
    }
}
