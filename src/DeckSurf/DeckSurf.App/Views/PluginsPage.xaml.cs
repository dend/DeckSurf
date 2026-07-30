using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using DeckSurf.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace DeckSurf.App.Views
{
    public sealed partial class PluginsPage : Page
    {
        public PluginsPage()
        {
            InitializeComponent();
        }

        public PluginsViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<PluginsViewModel>();

        private void PluginRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PluginInfo plugin })
            {
                // Subdued by design: page changes are instant, like Windows Settings.
                Frame.Navigate(typeof(PluginDetailPage), plugin.Id, new SuppressNavigationTransitionInfo());
            }
        }
    }
}
