using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeckSurf.App.Views
{
    /// <summary>
    /// The sub-page for one plugin: breadcrumb back to the plugin list, the
    /// plugin's facts, and its command tiles.
    /// </summary>
    public sealed partial class PluginDetailPage : Page
    {
        private readonly PluginService pluginService = Ioc.Default.GetRequiredService<PluginService>();

        public PluginDetailPage()
        {
            InitializeComponent();
        }

        public PluginInfo? Plugin { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Plugin = e.Parameter is string pluginId ? pluginService.GetPlugin(pluginId) : null;
            if (Plugin is null)
            {
                // The plugin vanished (rescan while navigating); nothing to show.
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }

                return;
            }

            Breadcrumb.ItemsSource = new[] { "Plugins", Plugin.DisplayName };
            Bindings.Update();
        }

        private void Breadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Index == 0 && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
