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

            RootNavigation.SelectedItem = DevicesItem;
        }

        private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                var pageType = tag switch
                {
                    "devices" => typeof(Views.DevicesPage),
                    "editor" => typeof(Views.ProfileEditorPage),
                    "plugins" => typeof(Views.PluginsPage),
                    "settings" => typeof(Views.SettingsPage),
                    _ => typeof(Views.DevicesPage),
                };

                if (ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }
    }
}
