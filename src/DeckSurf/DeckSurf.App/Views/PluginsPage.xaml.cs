using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Views
{
    public sealed partial class PluginsPage : Page
    {
        public PluginsPage()
        {
            InitializeComponent();
        }

        public PluginsViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<PluginsViewModel>();
    }
}
