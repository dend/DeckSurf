using CommunityToolkit.Mvvm.ComponentModel;
using DeckSurf.SDK.Models;
using System.Collections.ObjectModel;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// A single editable parameter in the auto-generated command settings form.
    /// The value is kept in its serialized string form.
    /// </summary>
    public partial class ParameterFieldViewModel : ObservableObject
    {
        public ParameterFieldViewModel(CommandParameterAttribute definition, string? initialValue)
        {
            Definition = definition;
            Value = initialValue ?? definition.DefaultValue;
        }

        public CommandParameterAttribute Definition { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoolValue))]
        [NotifyPropertyChangedFor(nameof(NumberValue))]
        public partial string? Value { get; set; }

        public string Key => Definition.Key;

        public string DisplayName => Definition.DisplayName ?? Definition.Key;

        // The Windows forms convention for required fields: an asterisk suffix,
        // not a parenthetical.
        public string HeaderText => Definition.Required ? $"{DisplayName} *" : DisplayName;

        public string Description => Definition.Description ?? string.Empty;

        public bool HasDescription => !string.IsNullOrEmpty(Definition.Description);

        public CommandParameterType Kind => Definition.ParameterType;

        public IReadOnlyList<string> Choices => Definition.Choices ?? [];

        public bool HasDynamicChoices => Definition.DynamicChoices;

        /// <summary>
        /// Gets runtime suggestions served by the command's choice provider.
        /// Suggestions only. The field stays editable, since the backing source
        /// may be offline while the profile is edited.
        /// </summary>
        public ObservableCollection<string> DynamicChoices { get; } = [];

        /// <summary>
        /// Replaces the suggestion list. Must be called on the UI thread.
        /// </summary>
        public void SetDynamicChoices(IReadOnlyList<string> choices)
        {
            DynamicChoices.Clear();
            foreach (var choice in choices)
            {
                DynamicChoices.Add(choice);
            }
        }

        public bool IsRequired => Definition.Required;

        public double Minimum => Definition.MinValue == int.MinValue ? 0 : Definition.MinValue;

        public double Maximum => Definition.MaxValue == int.MaxValue ? double.MaxValue : Definition.MaxValue;

        /// <summary>
        /// Gets or sets the value as a boolean, for ToggleSwitch binding.
        /// </summary>
        public bool BoolValue
        {
            get => bool.TryParse(Value, out var parsed) && parsed;
            set => Value = value ? "true" : "false";
        }

        /// <summary>
        /// Gets or sets the value as a number, for NumberBox binding. NaN represents "unset".
        /// </summary>
        public double NumberValue
        {
            get => double.TryParse(Value, out var parsed) ? parsed : double.NaN;
            set => Value = double.IsNaN(value) ? null : ((long)value).ToString();
        }
    }
}
