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
        private const int MaxTileChips = 2;

        /// <summary>
        /// Gets structured rows for the settings flyout. Built once per CommandInfo;
        /// x:Bind reads the property repeatedly, so it must not re-project per access.
        /// </summary>
        public IReadOnlyList<ParameterRow> ParameterRows { get; } = BuildParameterRows(Parameters);

        public IReadOnlyList<string> ModelChips => CompatibleModels.Count is 0 or >= AllKnownModelCount
            ? ["All models"]
            : [.. CompatibleModels.Select(m => m.ToString())];

        /// <summary>
        /// Gets at most two chips for the tile footer; more collapse into a "+N"
        /// overflow chip. The tooltip and the flyout carry the full list.
        /// </summary>
        public IReadOnlyList<string> TileChips
        {
            get
            {
                var chips = ModelChips;
                return chips.Count <= MaxTileChips ? chips : [chips[0], $"+{chips.Count - 1}"];
            }
        }

        public string DevicesToolTip => $"Supported devices: {string.Join(", ", ModelChips)}";

        public bool HasParameters => Parameters.Count > 0;

        public bool HasNoParameters => Parameters.Count == 0;

        public bool HasDescription => !string.IsNullOrEmpty(Description);

        /// <summary>
        /// Gets null (never empty) so no blank tooltip appears when command
        /// metadata could not be read (Description defaults to string.Empty).
        /// </summary>
        public string? DescriptionToolTip => string.IsNullOrEmpty(Description) ? null : Description;

        public string SettingsSummary => Parameters.Count == 1 ? "1 setting" : $"{Parameters.Count} settings";

        /// <summary>
        /// Gets the Narrator name: distinguishes tiles from their visible label and
        /// tells the user what invoking will reveal.
        /// </summary>
        public string AccessibleName => HasParameters
            ? $"{DisplayName}, {SettingsSummary}"
            : $"{DisplayName}, no settings";

        private static IReadOnlyList<ParameterRow> BuildParameterRows(IReadOnlyList<CommandParameterAttribute> parameters) =>
            [.. parameters.Select(p =>
            {
                var typeLabel = p.ParameterType switch
                {
                    CommandParameterType.String => "Text",
                    CommandParameterType.Integer => "Number",
                    CommandParameterType.Boolean => "On/off",
                    CommandParameterType.Choice => "Choice",
                    CommandParameterType.FilePath => "File path",
                    CommandParameterType.FolderPath => "Folder path",
                    CommandParameterType.ImagePath => "Image file",
                    CommandParameterType.DurationSeconds => "Duration in seconds",
                    _ => p.ParameterType.ToString(),
                };

                // Choices is null unless declared on the attribute; guard it.
                IReadOnlyList<ChoiceChip> choiceChips = p.Choices is { Length: > 0 }
                    ? [.. p.Choices.Select(c => new ChoiceChip(
                        c, string.Equals(c, p.DefaultValue, StringComparison.OrdinalIgnoreCase)))]
                    : [];

                // The default renders as the accent chip when it matches a declared
                // choice; otherwise (free-form default, or a default naming no choice)
                // it falls back to a caption badge so the value is never dropped.
                var defaultBadge = !string.IsNullOrEmpty(p.DefaultValue) && !choiceChips.Any(c => c.IsDefault)
                    ? $"Default: {p.DefaultValue}"
                    : null;

                return new ParameterRow(
                    p.DisplayName ?? p.Key,
                    typeLabel,
                    p.Required,
                    choiceChips,
                    defaultBadge,
                    string.IsNullOrEmpty(p.Description) ? null : p.Description);
            })];
    }

    /// <summary>
    /// One choice value; the default choice renders as an accent pill.
    /// IsNotDefault exists because x:Bind has no inline negation.
    /// </summary>
    public sealed record ChoiceChip(string Label, bool IsDefault)
    {
        public bool IsNotDefault => !IsDefault;
    }

    /// <summary>
    /// One declared command parameter, shaped for the spec-sheet flyout.
    /// Description stays null (not empty) when there is no help text so the help
    /// TextBlock collapses via bool-to-Visibility x:Bind.
    /// </summary>
    public sealed record ParameterRow(
        string Name,
        string TypeLabel,
        bool IsRequired,
        IReadOnlyList<ChoiceChip> ChoiceChips,
        string? DefaultBadge,
        string? Description)
    {
        public bool HasChoices => ChoiceChips.Count > 0;

        public bool HasStandaloneDefault => DefaultBadge is not null;

        public bool HasDescription => Description is not null;
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
            ? $"Version {Version} by {Author}"
            : $"{Id}, version {Version} by {Author}";
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
