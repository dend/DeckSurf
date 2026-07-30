using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Views
{
    public sealed partial class ActivityPage : Page
    {
        public ActivityPage()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public ActivityViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<ActivityViewModel>();
    }
}
