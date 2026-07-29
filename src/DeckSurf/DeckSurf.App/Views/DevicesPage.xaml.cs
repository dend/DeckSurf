using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DeckSurf.App.Views
{
    public sealed partial class DevicesPage : Page
    {
        public DevicesPage()
        {
            InitializeComponent();
        }

        public DevicesViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<DevicesViewModel>();

        private void BrightnessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: DeviceItemViewModel item }
                && item.ApplyBrightnessCommand.CanExecute(null))
            {
                item.ApplyBrightnessCommand.Execute(null);
            }
        }
    }
}
