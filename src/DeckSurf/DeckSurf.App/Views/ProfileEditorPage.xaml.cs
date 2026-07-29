using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using DeckSurf.App.ViewModels;
using DeckSurf.SDK.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace DeckSurf.App.Views
{
    public sealed partial class ProfileEditorPage : Page
    {
        // Logical width of one key tile slot; matches ItemsWrapGrid.ItemWidth.
        private const int TileSlotWidth = 96;

        // Chrome around the grid: nav pane (220), page padding (48), deck card padding,
        // column gap, and the key-configuration inspector in its expanded state.
        private const int NonGridWidth = 760;

        private const double InspectorExpandedWidth = 380;
        private const double InspectorCollapsedWidth = 52;

        private bool inspectorCollapsed;

        public ProfileEditorPage()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.GridColumns))
                {
                    ApplyGridColumns();
                }
            };
            ViewModel.RefreshProfiles();
        }

        public ProfileEditorViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<ProfileEditorViewModel>();

        private void KeyGrid_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyGridColumns();
        }

        // The editor grid must always show exactly the device's column count so the
        // on-screen arrangement matches the physical key layout. The grid gets a hard
        // width (slots are a fixed 96px) so it can never re-wrap, and the window
        // cannot shrink below one full row of keys plus the expanded inspector.
        private void ApplyGridColumns()
        {
            if (ViewModel.GridColumns <= 0)
            {
                return;
            }

            if (KeyGrid.ItemsPanelRoot is ItemsWrapGrid panel)
            {
                panel.MaximumRowsOrColumns = ViewModel.GridColumns;
            }

            KeyGrid.Width = (ViewModel.GridColumns * TileSlotWidth) + 4;

            // The any-key strip shares the deck card's width so the two read as one
            // cohesive stage (grid width + card padding + border).
            AnyKeyCard.Width = KeyGrid.Width + 30;

            Ioc.Default.GetRequiredService<WindowService>().SetMinimumSize(
                (ViewModel.GridColumns * TileSlotWidth) + NonGridWidth,
                560);
        }

        private void ToggleInspector_Click(object sender, RoutedEventArgs e)
        {
            inspectorCollapsed = !inspectorCollapsed;

            InspectorColumn.Width = new GridLength(inspectorCollapsed ? InspectorCollapsedWidth : InspectorExpandedWidth);
            InspectorBody.Visibility = inspectorCollapsed ? Visibility.Collapsed : Visibility.Visible;
            InspectorTitle.Visibility = inspectorCollapsed ? Visibility.Collapsed : Visibility.Visible;
            InspectorToggleGlyph.Glyph = inspectorCollapsed ? "\uE76B" : "\uE76C";
            ToolTipService.SetToolTip(InspectorToggle, inspectorCollapsed ? "Expand panel" : "Collapse panel");
        }

        // All target lists (keys, catch-alls, knobs, screen) share single-selection:
        // selecting in one clears the others and drives the inspector.
        private void TargetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not GridView active)
            {
                return;
            }

            GridView[] allLists = [KeyGrid, CatchAllList, KnobList, ScreenList];

            if (active.SelectedItem is KeyViewModel key)
            {
                foreach (var list in allLists)
                {
                    if (!ReferenceEquals(list, active))
                    {
                        list.SelectedItem = null;
                    }
                }

                ViewModel.SelectedKey = key;
            }
            else if (allLists.All(list => list.SelectedItem is null))
            {
                ViewModel.SelectedKey = null;
            }
        }

        private async void NewProfile_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox
            {
                PlaceholderText = "Profile name",
            };

            var dialog = new ContentDialog
            {
                Title = "New profile",
                Content = nameBox,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                ViewModel.CreateProfile(nameBox.Text.Trim());
            }
        }

        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProfileName is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = $"Delete profile '{ViewModel.SelectedProfileName}'?",
                Content = "The profile and its button mappings are removed permanently.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.DeleteSelectedProfile();
            }
        }

        private async void BrowseKeyImage_Click(object sender, RoutedEventArgs e)
        {
            var path = await PickFileAsync([".png", ".jpg", ".jpeg", ".bmp", ".gif"]);
            if (path is not null)
            {
                ViewModel.SetSelectedKeyImagePath(path);
            }
        }

        private async void BrowseParameterFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: ParameterFieldViewModel field })
            {
                return;
            }

            var extensions = field.Kind == CommandParameterType.ImagePath
                ? new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }
                : ["*"];

            var path = await PickFileAsync(extensions);
            if (path is not null)
            {
                field.Value = path;
            }
        }

        private async Task<string?> PickFileAsync(string[] extensions)
        {
            var picker = new FileOpenPicker();
            foreach (var extension in extensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            Ioc.Default.GetRequiredService<WindowService>().InitializePicker(picker);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
