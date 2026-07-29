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

        public IReadOnlyList<string> ModelChips => CompatibleModels.Count is 0 or >= AllKnownModelCount
            ? ["All models"]
            : [.. CompatibleModels.Select(m => m.ToString())];

        public string ParametersText => Parameters.Count == 0
            ? "No settings"
            : "Settings: " + string.Join(", ", Parameters.Select(p => p.DisplayName ?? p.Key));

        public string MetaText => $"{ModelsText} · {ParametersText}";
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
        string SourcePath,
        IReadOnlyList<CommandInfo> Commands)
    {
        // Avoid repeating the id when no distinct display name is set.
        public string Subtitle => string.Equals(DisplayName, Id, StringComparison.Ordinal)
            ? $"v{Version} · {Author}"
            : $"{Id} · v{Version} · {Author}";
    }

    /// <summary>
    /// Discovers plugins from the built-in locations (next to the application and the
    /// sibling CLI output) and any user-configured folders, and caches their command
    /// metadata. Rescans pick up newly added plugin assemblies; assemblies already
    /// loaded stay in memory until the app restarts.
    /// </summary>
    public sealed class PluginService
    {
        private readonly AppSettingsService appSettings;
        private readonly object loadLock = new();
        private readonly List<string> diagnostics = [];

        private List<PluginInfo>? plugins;

        public PluginService(AppSettingsService appSettings)
        {
            this.appSettings = appSettings;
        }

        /// <summary>
        /// Raised after a rescan changes the plugin list. Raised on the calling thread.
        /// </summary>
        public event EventHandler? PluginsChanged;

        public IReadOnlyList<PluginInfo> Plugins
        {
            get
            {
                EnsureLoaded();
                return plugins!;
            }
        }

        /// <summary>
        /// Gets non-fatal plugin load warnings collected during discovery.
        /// </summary>
        public IReadOnlyList<string> Diagnostics
        {
            get
            {
                EnsureLoaded();
                return diagnostics;
            }
        }

        /// <summary>
        /// Gets the built-in directories probed for plugins, in priority order.
        /// </summary>
        public IReadOnlyList<string> BuiltInDirectories
        {
            get
            {
                var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var parentDirectory = Path.GetDirectoryName(baseDirectory);
                return parentDirectory is null ? [baseDirectory] : [baseDirectory, parentDirectory];
            }
        }

        /// <summary>
        /// Gets the user-configured plugin folders.
        /// </summary>
        public IReadOnlyList<string> CustomDirectories => [.. appSettings.PluginDirectories];

        public PluginInfo? GetPlugin(string pluginId)
        {
            return Plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        }

        public CommandInfo? GetCommand(string pluginId, string commandId)
        {
            return GetPlugin(pluginId)?.Commands
                .FirstOrDefault(c => string.Equals(c.Id, commandId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Rescans all plugin locations and notifies listeners.
        /// </summary>
        public void Reload()
        {
            lock (loadLock)
            {
                plugins = LoadAllPlugins();
            }

            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureLoaded()
        {
            lock (loadLock)
            {
                plugins ??= LoadAllPlugins();
            }
        }

        private List<PluginInfo> LoadAllPlugins()
        {
            diagnostics.Clear();

            var loaded = new List<(IDeckSurfPlugin Plugin, string Source)>();

            // Built-in locations: the app's own directory first (plugins\ shipped next
            // to the exe), then the parent directory so the dev layout finds the Barn
            // plugin in src\bin\plugins produced by the CLI build.
            foreach (var directory in BuiltInDirectories)
            {
                foreach (var plugin in PluginLoader.LoadPlugins(directory, diagnostics.Add))
                {
                    loaded.Add((plugin, directory));
                }

                if (loaded.Count > 0)
                {
                    break;
                }
            }

            // User-configured folders are scanned recursively so a folder-per-plugin
            // layout works without a literal "plugins" directory name.
            foreach (var directory in appSettings.PluginDirectories)
            {
                if (!Directory.Exists(directory))
                {
                    diagnostics.Add($"Plugin folder not found: {directory}");
                    continue;
                }

                foreach (var plugin in PluginLoader.LoadPlugins(directory, diagnostics.Add, scanBaseRecursively: true))
                {
                    loaded.Add((plugin, directory));
                }
            }

            // First occurrence of a plugin id wins so built-in copies take priority
            // over duplicates in user folders.
            var result = new List<PluginInfo>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (plugin, source) in loaded)
            {
                var id = plugin.Metadata?.Id;
                if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
                {
                    continue;
                }

                result.Add(CreatePluginInfo(plugin, source));
            }

            return result;
        }

        private PluginInfo CreatePluginInfo(IDeckSurfPlugin plugin, string sourcePath)
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
                sourcePath,
                commands);
        }
    }
}
