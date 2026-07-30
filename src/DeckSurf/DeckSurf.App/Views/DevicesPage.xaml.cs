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

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Profiles may have been created, renamed, or activated in the
            // editor since the page was last shown.
            ViewModel.RefreshProfiles();
        }
    }
}
