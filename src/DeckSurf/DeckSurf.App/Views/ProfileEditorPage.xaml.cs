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

            // Edits save themselves; Ctrl+S has nothing left to do.
            RegisterAccelerator(Windows.System.VirtualKey.N, Windows.System.VirtualKeyModifiers.Control, () => NewProfile_Click(this, new RoutedEventArgs()));
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

            // Everything on the stage shares the 96px key pitch. Strips span the full
            // pitch width; their items carry the same uniform 4px margin as the key
            // tiles, so all visible edges land on the tiles' visible edges. The grid
            // gets 2px of slack against fractional-DPI rounding.
            var pitchWidth = ViewModel.GridColumns * TileSlotWidth;
            KeyGrid.Width = pitchWidth + 2;
            ScreenRow.Width = pitchWidth;
            CatchAllList.Width = pitchWidth;
        }

        // Drag and drop moves a full mapping between two targets of the same kind:
        // key to key, knob to knob. The source key returns to its blank state.
        // Strips and the any-key slot stay put.
        private KeyViewModel? dragSource;

        private void TargetList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count == 1
                && e.Items[0] is KeyViewModel key
                && (key.Target == MappingTarget.Key || key.Target == MappingTarget.Knob)
                && key.Index >= 0)
            {
                dragSource = key;
                e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void Target_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation =
                dragSource is not null
                && sender is FrameworkElement { DataContext: KeyViewModel target }
                && !ReferenceEquals(target, dragSource)
                && target.Target == dragSource.Target
                && target.Index >= 0
                    ? Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move
                    : Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
        }

        private void Target_Drop(object sender, DragEventArgs e)
        {
            if (dragSource is not null
                && sender is FrameworkElement { DataContext: KeyViewModel target })
            {
                ViewModel.MoveMapping(dragSource, target);
                e.Handled = true;
            }

            dragSource = null;
        }

        // All target lists (keys, catch-alls, knobs, screen) share single-selection:
        // selecting in one clears the others and drives the inspector.
        private void TargetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListViewBase active)
            {
                return;
            }

            ListViewBase[] allLists = [KeyGrid, CatchAllList, KnobList, ScreenList, TouchLeftList, TouchRightList];

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

        // The dynamic-choice field acts like a dropdown on focus (full suggestion
        // list) and like a search box while typing (filtered list). The list is
        // fed per-field from the command's choice provider.
        private void DynamicChoice_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is AutoSuggestBox box && box.DataContext is ParameterFieldViewModel field && field.DynamicChoices.Count > 0)
            {
                box.ItemsSource = field.DynamicChoices;
                box.IsSuggestionListOpen = true;
            }
        }

        private void DynamicChoice_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput
                || sender.DataContext is not ParameterFieldViewModel field)
            {
                return;
            }

            sender.ItemsSource = string.IsNullOrEmpty(sender.Text)
                ? field.DynamicChoices
                : field.DynamicChoices.Where(choice => choice.Contains(sender.Text, StringComparison.OrdinalIgnoreCase)).ToList();
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
