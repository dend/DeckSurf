using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Services;

namespace DeckSurf.App.ViewModels
{
    public partial class PluginsViewModel : ObservableObject
    {
        private readonly PluginService pluginService;

        public PluginsViewModel(PluginService pluginService)
        {
            this.pluginService = pluginService;
        }

        public IReadOnlyList<PluginInfo> Plugins => pluginService.Plugins;

        public bool HasNoPlugins => Plugins.Count == 0;

        public bool HasDiagnostics => pluginService.Diagnostics.Count > 0;

        public string DiagnosticsText => string.Join(Environment.NewLine, pluginService.Diagnostics);

        [RelayCommand]
        private void OpenPluginsFolder()
        {
            var pluginsPath = Path.Combine(pluginService.ProbeDirectories[0], "plugins");
            var target = Directory.Exists(pluginsPath)
                ? pluginsPath
                : pluginService.ProbeDirectories.Select(d => Path.Combine(d, "plugins")).FirstOrDefault(Directory.Exists)
                    ?? pluginsPath;

            Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
    }
}
