using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// One key tile in the profile editor grid. Index -1 represents a catch-all
    /// mapping that fires for every button press.
    /// </summary>
    public partial class KeyViewModel : ObservableObject
    {
        public KeyViewModel(int index)
        {
            Index = index;
        }

        public int Index { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapping))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        public partial string? PluginId { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapping))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        public partial string? CommandId { get; set; }

        [ObservableProperty]
        public partial string? CommandArguments { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewImage))]
        [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        public partial string? ImagePath { get; set; }

        public string IndexLabel => Index == -1 ? "Any" : Index.ToString();

        public bool HasMapping => !string.IsNullOrEmpty(PluginId) && !string.IsNullOrEmpty(CommandId);

        public string Label => CommandId ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether the tile shows its text label. Hidden when a
        /// custom image occupies the key face.
        /// </summary>
        public bool ShowLabel => HasMapping && !HasPreviewImage;

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
