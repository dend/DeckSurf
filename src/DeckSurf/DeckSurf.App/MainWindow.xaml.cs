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

            // Acrylic first: the transparent navigation pane then reads as frosted
            // glass over the desktop, while pages stay on their opaque layer.
            if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            else if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop();
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
                // Subdued by design: switching pages is instant, like Windows Settings.
                ContentFrame.Navigate(pageType, null, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
            }
        }

        // The runtime is automatic: while a device is connected, its active profile
        // runs on it. The dot informs, hover carries which profile runs where, and
        // the click opens Activity.
        private void UpdateRuntimeStatus(RuntimeService runtimeService)
        {
            var activeSessions = runtimeService.ActiveSessions;
            string toolTip;
            if (activeSessions.Count > 0)
            {
                RuntimeStatusDot.Fill = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                RuntimeStatusText.Text = activeSessions.Count == 1
                    ? $"Active on {activeSessions[0].DeviceName}"
                    : $"Active on {activeSessions.Count} devices";
                toolTip = string.Join(
                    Environment.NewLine,
                    activeSessions.Select(s => $"{s.ProfileName} on {s.DeviceName}"));
            }
            else
            {
                RuntimeStatusDot.Fill = (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
                RuntimeStatusText.Text = "Idle";
                toolTip = "No profiles running. Connect a device, or choose a profile in the editor.";
            }

            ToolTipService.SetToolTip(RuntimeStatusButton, toolTip);
        }

        private void RuntimeStatus_Click(object sender, RoutedEventArgs e)
        {
            RootNavigation.SelectedItem = ActivityItem;
        }
    }
}
