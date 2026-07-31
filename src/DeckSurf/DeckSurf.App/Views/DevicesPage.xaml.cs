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

        // Tag carries the item view model; ItemsRepeater templates do not flow
        // DataContext, so the binding is explicit.
        private async void RenameDevice_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is not Microsoft.UI.Xaml.FrameworkElement { Tag: DeviceItemViewModel item })
            {
                return;
            }

            var nameBox = new TextBox
            {
                Text = item.Nickname ?? string.Empty,
                PlaceholderText = item.Name,
                SelectionStart = item.Nickname?.Length ?? 0,
            };

            var dialog = new ContentDialog
            {
                Title = "Device nickname",
                Content = nameBox,
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.SetNickname(item, nameBox.Text);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                ViewModel.SetNickname(item, null);
            }
        }
    }
}
