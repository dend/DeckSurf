using CommunityToolkit.Mvvm.ComponentModel;
using DeckSurf.SDK.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// One mappable target tile in the profile editor: a key on the grid (index -1
    /// is the any-key catch-all), a knob, or the touch screen.
    /// </summary>
    public partial class KeyViewModel : ObservableObject
    {
        public KeyViewModel(int index, MappingTarget target = MappingTarget.Key)
        {
            Index = index;
            Target = target;
        }

        public int Index { get; }

        public MappingTarget Target { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapping))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        public partial string? PluginId { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapping))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        public partial string? CommandId { get; set; }

        [ObservableProperty]
        public partial string? CommandArguments { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewImage))]
        [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        public partial string? ImagePath { get; set; }

        public string IndexLabel => Target switch
        {
            MappingTarget.Knob => $"Knob {Index + 1}",
            MappingTarget.Screen => "Screen",
            _ => Index == -1 ? "Any" : Index.ToString(),
        };

        public bool HasMapping => !string.IsNullOrEmpty(PluginId) && !string.IsNullOrEmpty(CommandId);

        public string Label => CommandId ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether the tile shows its text label. Hidden when a
        /// custom image occupies the key face.
        /// </summary>
        public bool ShowLabel => HasMapping && !HasPreviewImage;

        /// <summary>
        /// Gets a value indicating whether the tile shows its unmapped placeholder.
        /// </summary>
        public bool ShowPlaceholder => !HasMapping && !HasPreviewImage;

        public bool HasPreviewImage => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);

        public ImageSource? PreviewImage
        {
            get
            {
                if (!HasPreviewImage)
                {
                    return null;
                }

                try
                {
                    return new BitmapImage(new Uri(ImagePath!));
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public void Clear()
        {
            PluginId = null;
            CommandId = null;
            CommandArguments = null;
            ImagePath = null;
        }
    }
}
