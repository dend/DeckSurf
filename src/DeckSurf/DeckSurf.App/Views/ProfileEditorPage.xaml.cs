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

            RegisterAccelerator(Windows.System.VirtualKey.S, Windows.System.VirtualKeyModifiers.Control, () =>
            {
                if (ViewModel.SaveCommand.CanExecute(null))
                {
                    ViewModel.SaveCommand.Execute(null);
                }
            });
            RegisterAccelerator(Windows.System.VirtualKey.N, Windows.System.VirtualKeyModifiers.Control, () => NewProfile_Click(this, new RoutedEventArgs()));
            RegisterAccelerator(Windows.System.VirtualKey.F5, Windows.System.VirtualKeyModifiers.None, () =>
            {
                if (ViewModel.ToggleRuntimeCommand.CanExecute(null))
                {
                    ViewModel.ToggleRuntimeCommand.Execute(null);
                }
            });
        }

        private void RegisterAccelerator(Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers modifiers, Action action)
        {
            var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (_, e) =>
            {
                e.Handled = true;
                action();
            };
            KeyboardAccelerators.Add(accelerator);
        }

        public ProfileEditorViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<ProfileEditorViewModel>();

        private void KeyGrid_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyGridColumns();
        }

        // The editor grid must always show exactly the device's column count so the
        // on-screen arrangement matches the physical key layout; the grid gets a hard
        // width (slots are a fixed 96px) so it can never re-wrap. The window itself is
        // width-locked at startup for the widest supported layout.
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
        }

        // All target lists (keys, catch-alls, knobs, screen) share single-selection:
        // selecting in one clears the others and drives the inspector.
        private void TargetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListViewBase active)
            {
                return;
            }

            ListViewBase[] allLists = [KeyGrid, CatchAllList, KnobList, ScreenList];

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
