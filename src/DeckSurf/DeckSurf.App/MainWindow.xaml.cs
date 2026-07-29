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

            RootNavigation.SelectedItem = DevicesItem;
        }

        private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            Type pageType;
            if (args.IsSettingsSelected)
            {
                pageType = typeof(Views.SettingsPage);
            }
            else if (args.SelectedItem is NavigationViewItem { Tag: string tag })
            {
                pageType = tag switch
                {
                    "editor" => typeof(Views.ProfileEditorPage),
                    "plugins" => typeof(Views.PluginsPage),
                    _ => typeof(Views.DevicesPage),
                };
            }
            else
            {
                return;
            }

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void UpdateRuntimeStatus(RuntimeService runtimeService)
        {
            if (runtimeService.IsRunning)
            {
                RuntimeStatusDot.Fill = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                RuntimeStatusText.Text = $"Running: {runtimeService.ActiveProfileName}";
            }
            else
            {
                RuntimeStatusDot.Fill = (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
                RuntimeStatusText.Text = "Stopped";
            }
        }
    }
}
