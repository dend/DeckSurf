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

        /// <summary>
        /// Gets or sets a value indicating whether this target is the one being
        /// configured. Drives the accent selection ring on the deck stage, which
        /// carries the selection visibly on the dark face.
        /// </summary>
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapping))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        public partial string? PluginId { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapping))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(TitleText))]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        public partial string? CommandId { get; set; }

        /// <summary>
        /// Gets or sets the command's friendly display name; tiles fall back to the
        /// raw command id only when the plugin is not loaded.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(TitleText))]
        public partial string? CommandDisplayName { get; set; }

        [ObservableProperty]
        public partial string? CommandArguments { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewImage))]
        [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
        [NotifyPropertyChangedFor(nameof(HasNoPreviewImage))]
        [NotifyPropertyChangedFor(nameof(ImageFileName))]
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

        public string Label => CommandDisplayName ?? CommandId ?? string.Empty;

        /// <summary>
        /// Gets the strip title for hardware targets: the target's identity plus the
        /// mapped command, or a placeholder when nothing is assigned yet.
        /// </summary>
        public string TitleText
        {
            get
            {
                if (!HasMapping)
                {
                    return "Not configured";
                }

                return Target switch
                {
                    MappingTarget.Screen => $"Touch screen: {Label}",
                    _ when Index == -1 => $"Any key: {Label}",
                    _ => Label,
                };
            }
        }

        /// <summary>
        /// Gets or sets the frame the physical key is currently displaying, mirrored
        /// from the running device. Cleared when the device's session ends.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowLabel))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        public partial ImageSource? LiveImage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the tile shows its text label. Hidden when a
        /// custom image or a live hardware frame occupies the key face.
        /// </summary>
        public bool ShowLabel => HasMapping && !HasPreviewImage && LiveImage is null;

        /// <summary>
        /// Gets a value indicating whether the tile shows its unmapped placeholder.
        /// </summary>
        public bool ShowPlaceholder => !HasMapping && !HasPreviewImage && LiveImage is null;

        public bool HasPreviewImage => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);

        public bool HasNoPreviewImage => !HasPreviewImage;

        public string ImageFileName => string.IsNullOrEmpty(ImagePath) ? string.Empty : Path.GetFileName(ImagePath);

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
