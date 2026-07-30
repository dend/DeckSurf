using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Views
{
    public sealed partial class DevicesPage : Page
    {
        public DevicesPage()
        {
            InitializeComponent();
        }

        public DevicesViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<DevicesViewModel>();
    }
}
