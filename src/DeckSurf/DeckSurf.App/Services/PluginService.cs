using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Metadata for a single plugin command, readable without keeping an instance alive.
    /// </summary>
    public sealed record CommandInfo(
        Type CommandType,
        string Id,
        string DisplayName,
        string Description,
        IReadOnlyList<CommandParameterAttribute> Parameters,
        IReadOnlyList<DeviceModel> CompatibleModels)
    {
        private const int AllKnownModelCount = 9;

        public string ModelsText => CompatibleModels.Count is 0 or >= AllKnownModelCount
            ? "All models"
            : string.Join(", ", CompatibleModels);

        public string ParametersText => Parameters.Count == 0
            ? "No settings"
            : string.Join(", ", Parameters.Select(p => p.DisplayName ?? p.Key));
    }

    /// <summary>
    /// Metadata for a loaded plugin and its commands.
    /// </summary>
    public sealed record PluginInfo(
        IDeckSurfPlugin Plugin,
        string Id,
        string DisplayName,
        string Version,
        string Author,
        IReadOnlyList<CommandInfo> Commands)
    {
        public string Subtitle => $"{Id} · v{Version} · {Author}";
    }

    /// <summary>
    /// Discovers plugins next to the application (release layout) or next to the
    /// sibling CLI output (dev layout) and caches their command metadata.
    /// </summary>
    public sealed class PluginService
    {
        private readonly List<string> diagnostics = [];
        private readonly Lazy<IReadOnlyList<PluginInfo>> plugins;

        public PluginService()
        {
            plugins = new Lazy<IReadOnlyList<PluginInfo>>(LoadAllPlugins);
        }

        public IReadOnlyList<PluginInfo> Plugins => plugins.Value;

        /// <summary>
        /// Gets non-fatal plugin load warnings collected during discovery.
        /// </summary>
        public IReadOnlyList<string> Diagnostics
        {
            get
            {
                _ = plugins.Value;
                return diagnostics;
            }
        }

        /// <summary>
        /// Gets the directories probed for plugins, in priority order.
        /// </summary>
        public IReadOnlyList<string> ProbeDirectories
        {
            get
            {
                var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var parentDirectory = Path.GetDirectoryName(baseDirectory);
                return parentDirectory is null ? [baseDirectory] : [baseDirectory, parentDirectory];
            }
        }

        public PluginInfo? GetPlugin(string pluginId)
        {
            return Plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        }

        public CommandInfo? GetCommand(string pluginId, string commandId)
        {
            return GetPlugin(pluginId)?.Commands
                .FirstOrDefault(c => string.Equals(c.Id, commandId, StringComparison.OrdinalIgnoreCase));
        }

        private IReadOnlyList<PluginInfo> LoadAllPlugins()
        {
            IReadOnlyList<IDeckSurfPlugin> loaded = [];

            // Probe the app's own directory first (plugins\ shipped next to the exe),
            // then fall back to the parent directory so the dev layout finds the Barn
            // plugin in src\bin\plugins produced by the CLI build.
            foreach (var directory in ProbeDirectories)
            {
                loaded = PluginLoader.LoadPlugins(directory, diagnostics.Add);
                if (loaded.Count > 0)
                {
                    break;
                }
            }

            return [.. loaded.Select(CreatePluginInfo)];
        }

        private PluginInfo CreatePluginInfo(IDeckSurfPlugin plugin)
        {
            var commands = new List<CommandInfo>();

            foreach (var commandType in PluginLoader.GetCommandTypes(plugin))
            {
                var displayName = commandType.Name;
                var description = string.Empty;

                // Name/Description are instance properties; instantiate transiently to
                // read them, then dispose immediately.
                try
                {
                    using var instance = (IDeckSurfCommand)Activator.CreateInstance(commandType)!;
                    displayName = instance.Name;
                    description = instance.Description;
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"Could not read metadata for command '{commandType.Name}': {ex.Message}");
                }

                var compatibleModels = commandType
                    .GetCustomAttributes(typeof(CompatibleWithAttribute), inherit: false)
                    .Cast<CompatibleWithAttribute>()
                    .Select(a => a.CompatibleModel)
                    .Distinct()
                    .ToList();

                commands.Add(new CommandInfo(
                    commandType,
                    commandType.Name,
                    displayName,
                    description,
                    CommandSchemaReader.GetParameters(commandType),
                    compatibleModels));
            }

            return new PluginInfo(
                plugin,
                plugin.Metadata.Id,
                string.IsNullOrEmpty(plugin.Metadata.Name) ? plugin.Metadata.Id : plugin.Metadata.Name,
                plugin.Metadata.Version,
                plugin.Metadata.Author,
                commands);
        }
    }
}
