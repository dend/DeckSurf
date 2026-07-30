using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using DeckSurf.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Views
{
    public sealed partial class PluginsPage : Page
    {
        public PluginsPage()
        {
            InitializeComponent();
        }

        public PluginsViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<PluginsViewModel>();

        // Full command details live in a dialog so the list rows stay compact.
        private async void CommandCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: CommandInfo command })
            {
                return;
            }

            var panel = new StackPanel { Spacing = 12, MinWidth = 380 };

            panel.Children.Add(new TextBlock
            {
                Text = command.Description,
                TextWrapping = TextWrapping.Wrap,
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"Supported devices: {command.ModelsText}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
            });

            if (command.Parameters.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "This command has no settings.",
                    Opacity = 0.7,
                });
            }
            else
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Settings",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                });

                foreach (var parameter in command.Parameters)
                {
                    var entry = new StackPanel { Spacing = 2 };
                    entry.Children.Add(new TextBlock { Text = parameter.DisplayName ?? parameter.Key });

                    var details = parameter.ParameterType.ToString();
                    if (parameter.Choices is { Length: > 0 })
                    {
                        details += $" ({string.Join(", ", parameter.Choices)})";
                    }

                    if (!string.IsNullOrEmpty(parameter.DefaultValue))
                    {
                        details += $", default {parameter.DefaultValue}";
                    }

                    if (parameter.Required)
                    {
                        details += ", required";
                    }

                    entry.Children.Add(new TextBlock { Text = details, FontSize = 12, Opacity = 0.6 });

                    if (!string.IsNullOrEmpty(parameter.Description))
                    {
                        entry.Children.Add(new TextBlock
                        {
                            Text = parameter.Description,
                            FontSize = 12,
                            Opacity = 0.6,
                            TextWrapping = TextWrapping.Wrap,
                        });
                    }

                    panel.Children.Add(entry);
                }
            }

            var dialog = new ContentDialog
            {
                Title = command.DisplayName,
                Content = new ScrollViewer { Content = panel, MaxHeight = 420 },
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };

            await dialog.ShowAsync();
        }
    }
}
