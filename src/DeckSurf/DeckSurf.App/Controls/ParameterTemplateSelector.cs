using DeckSurf.App.ViewModels;
using DeckSurf.SDK.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Controls
{
    /// <summary>
    /// Picks the input control template for a command parameter based on its declared type.
    /// </summary>
    public partial class ParameterTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }

        public DataTemplate? NumberTemplate { get; set; }

        public DataTemplate? BooleanTemplate { get; set; }

        public DataTemplate? ChoiceTemplate { get; set; }

        public DataTemplate? FileTemplate { get; set; }

        public DataTemplate? ImageTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is not ParameterFieldViewModel field)
            {
                return base.SelectTemplateCore(item, container);
            }

            return field.Kind switch
            {
                CommandParameterType.Integer or CommandParameterType.DurationSeconds => NumberTemplate,
                CommandParameterType.Boolean => BooleanTemplate,
                CommandParameterType.Choice => ChoiceTemplate,
                CommandParameterType.FilePath or CommandParameterType.FolderPath => FileTemplate,
                CommandParameterType.ImagePath => ImageTemplate,
                _ => TextTemplate,
            } ?? TextTemplate;
        }
    }
}
