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
        IReadOnlyList<DeviceModel> CompatibleModels,
        string? IconPath,
        string PluginName)
    {
        private static readonly int AllKnownModelCount = Enum.GetValues<DeviceModel>().Length;

        /// <summary>
        /// Gets structured rows for the detail flyout. Built once per CommandInfo;
        /// x:Bind reads the property repeatedly, so it must not re-project per access.
        /// </summary>
        public IReadOnlyList<ParameterRow> ParameterRows { get; } = BuildParameterRows(Parameters);

        public bool HasIcon => IconPath is not null;

        /// <summary>
        /// Gets the author-declared icon source, or null when the command renders
        /// as a backlit monogram cap instead.
        /// </summary>
        public Uri? IconUri => IconPath is null ? null : new Uri(IconPath);

        /// <summary>
        /// Gets the models this command is restricted to. Empty means the command
        /// works on every device.
        /// </summary>
        public IReadOnlyList<DeviceModel> RestrictedModels =>
            CompatibleModels.Count == 0 || CompatibleModels.Count >= AllKnownModelCount
                ? []
                : CompatibleModels;

        /// <summary>
        /// Gets the compatibility pills for the detail flyout, the only surface
        /// that states compatibility visually. Always populated: universal
        /// commands carry one "All devices" pill. Short model names keep the
        /// row compact under the "Works with" label.
        /// </summary>
        public IReadOnlyList<string> CompatibilityPills => RestrictedModels.Count == 0
            ? ["All devices"]
            : [.. RestrictedModels.Select(ShortName)];

        public bool HasParameters => Parameters.Count > 0;

        public bool HasNoParameters => Parameters.Count == 0;

        /// <summary>
        /// Gets a value indicating whether the command renders its own key or
        /// screen content at runtime, making a static button image meaningless.
        /// </summary>
        public bool HasDynamicDisplay { get; } =
            CommandType.IsDefined(typeof(CommandDynamicDisplayAttribute), inherit: true);

        public bool HasDescription => !string.IsNullOrEmpty(Description);

        /// <summary>
        /// Gets null (never empty) so no blank tooltip appears when command
        /// metadata could not be read (Description defaults to string.Empty).
        /// </summary>
        public string? DescriptionToolTip => string.IsNullOrEmpty(Description) ? null : Description;

        /// <summary>
        /// Gets the Narrator name. The tile face shows no settings indicator, so
        /// the spoken summary carries name, compatibility, and the settings phrase.
        /// </summary>
        public string AccessibleName =>
            $"{DisplayName}, works with {(RestrictedModels.Count == 0 ? "all devices" : JoinWithAnd([.. RestrictedModels.Select(ShortName)]))}, {SettingsPhrase}";

        private string SettingsPhrase => Parameters.Count switch
        {
            0 => "no settings",
            1 => "1 setting",
            var n => $"{n} settings",
        };

        private static string ShortName(DeviceModel model) => model switch
        {
            DeviceModel.Original => "Original",
            DeviceModel.Original2019 => "Original (2019)",
            DeviceModel.MK2 => "MK.2",
            DeviceModel.MK2Scissor => "MK.2 Scissor",
            DeviceModel.Mini => "Mini",
            DeviceModel.Mini2022 => "Mini (2022)",
            DeviceModel.XL => "XL",
            DeviceModel.XL2022 => "XL (2022)",
            DeviceModel.Plus => "Plus",
            DeviceModel.Neo => "Neo",
            _ => model.ToString(),
        };

        private static string FullName(DeviceModel model) => $"Stream Deck {ShortName(model)}";

        private static string JoinWithAnd(IReadOnlyList<string> parts) => parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))}, and {parts[^1]}",
        };

        private static IReadOnlyList<ParameterRow> BuildParameterRows(IReadOnlyList<CommandParameterAttribute> parameters) =>
            [.. parameters.Select(p =>
            {
                // The pill is the declared type, verbatim. The SDK enum is the
                // single vocabulary; the app does not maintain a translation.
                var typeLabel = p.ParameterType.ToString();

                // Every token comes straight from the declared attribute data;
                // nothing is inferred or embellished. A default matching a declared
                // option is marked on that option's pill; a free-form default gets
                // its own caption line so the value is never dropped.
                IReadOnlyList<string> options = p.Choices is { Length: > 0 }
                    ? [.. p.Choices.Select(c =>
                        string.Equals(c, p.DefaultValue, StringComparison.OrdinalIgnoreCase) ? $"{c} (default)" : c)]
                    : [];

                var defaultLine = !string.IsNullOrEmpty(p.DefaultValue)
                    && !(p.Choices?.Any(c => string.Equals(c, p.DefaultValue, StringComparison.OrdinalIgnoreCase)) ?? false)
                    ? $"Default: {p.DefaultValue}"
                    : null;

                return new ParameterRow(
                    p.DisplayName ?? p.Key,
                    typeLabel,
                    p.Required,
                    options,
                    defaultLine,
                    string.IsNullOrEmpty(p.Description) ? null : p.Description);
            })];
    }

    /// <summary>
    /// One declared command parameter, shaped for the detail flyout's form-preview
    /// blocks: name with type and required pills, option pills, and optional
    /// default and help lines that stay null (not empty) so their elements
    /// collapse via bool-to-Visibility x:Bind.
    /// </summary>
    public sealed record ParameterRow(
        string Name,
        string TypeLabel,
        bool IsRequired,
        IReadOnlyList<string> Options,
        string? DefaultLine,
        string? Description)
    {
        public bool HasOptions => Options.Count > 0;

        public bool HasDefault => DefaultLine is not null;

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
        /// <summary>
        /// Gets the plugin metadata for the section-header tooltip. Author,
        /// version, and assembly identity are plugin detail, not browse content;
        /// the header itself carries only the name.
        /// </summary>
        public string HeaderToolTip => $"{Author}{Environment.NewLine}Version {Version}{Environment.NewLine}{Id}";

        public string CommandCountText => Commands.Count == 1 ? "1 command" : $"{Commands.Count} commands";
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
            var gathered = new List<(Type CommandType, string DisplayName, string Description)>();

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

                gathered.Add((commandType, displayName, description));
            }

            var pluginDisplayName = string.IsNullOrEmpty(plugin.Metadata.Name) ? plugin.Metadata.Id : plugin.Metadata.Name;

            var commands = new List<CommandInfo>();
            foreach (var (commandType, displayName, description) in gathered)
            {
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
                    compatibleModels,
                    PluginLoader.GetCommandIconPath(commandType),
                    pluginDisplayName));
            }

            return new PluginInfo(
                plugin,
                plugin.Metadata.Id,
                pluginDisplayName,
                plugin.Metadata.Version,
                plugin.Metadata.Author,
                sourcePath,
                commands);
        }

    }
}
