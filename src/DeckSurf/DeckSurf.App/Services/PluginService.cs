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
        string Monogram,
        string PluginName)
    {
        private static readonly int AllKnownModelCount = Enum.GetValues<DeviceModel>().Length;

        /// <summary>
        /// Gets structured rows for the detail flyout. Built once per CommandInfo;
        /// x:Bind reads the property repeatedly, so it must not re-project per access.
        /// </summary>
        public IReadOnlyList<ParameterRow> ParameterRows { get; } = BuildParameterRows(Parameters);

        public bool HasIcon => IconPath is not null;

        public bool HasNoIcon => IconPath is null;

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
        /// Gets the always-populated tile footer line. Silence never encodes
        /// meaning: every tile states its compatibility in the same place.
        /// </summary>
        public string CompatibilityText => RestrictedModels.Count switch
        {
            0 => "Works with all devices",
            1 => $"Works with {ShortName(RestrictedModels[0])}",
            2 => $"Works with {ShortName(RestrictedModels[0])} and {ShortName(RestrictedModels[1])}",
            _ => $"Works with {ShortName(RestrictedModels[0])}, {ShortName(RestrictedModels[1])}, and {RestrictedModels.Count - 2} more",
        };

        /// <summary>
        /// Gets the full-name compatibility value for the flyout's fact block.
        /// </summary>
        public string CompatibilityFull => RestrictedModels.Count == 0
            ? "All devices"
            : JoinWithAnd([.. RestrictedModels.Select(FullName)]);

        public string DevicesToolTip => RestrictedModels.Count == 0
            ? "Supported devices: all models"
            : $"Supported devices: {string.Join(", ", RestrictedModels.Select(FullName))}";

        public bool HasParameters => Parameters.Count > 0;

        public bool HasNoParameters => Parameters.Count == 0;

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

                // Every token comes straight from the declared attribute data;
                // nothing is inferred or embellished.
                var meta = typeLabel;
                if (p.Required)
                {
                    meta += ", required";
                }

                if (!string.IsNullOrEmpty(p.DefaultValue))
                {
                    meta += $", default {p.DefaultValue}";
                }

                var options = p.Choices is { Length: > 0 }
                    ? $"Options: {string.Join(", ", p.Choices)}"
                    : null;

                return new ParameterRow(
                    p.DisplayName ?? p.Key,
                    meta,
                    options,
                    string.IsNullOrEmpty(p.Description) ? null : p.Description);
            })];
    }

    /// <summary>
    /// One declared command parameter, shaped for the detail flyout's form-preview
    /// blocks. Optional lines stay null (not empty) so their TextBlocks collapse
    /// via bool-to-Visibility x:Bind.
    /// </summary>
    public sealed record ParameterRow(
        string Name,
        string MetaLine,
        string? OptionsLine,
        string? Description)
    {
        public bool HasOptions => OptionsLine is not null;

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
        public string Subtitle => $"{Author}, version {Version}";

        /// <summary>
        /// Gets the developer identity, shown only in the section-header tooltip.
        /// </summary>
        public string HeaderToolTip => Id;
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
            // First pass gathers metadata; monograms need sibling awareness (shared
            // leading words carry no discriminating information within a section).
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

            var monograms = DeriveMonograms(gathered);
            var pluginDisplayName = string.IsNullOrEmpty(plugin.Metadata.Name) ? plugin.Metadata.Id : plugin.Metadata.Name;

            var commands = new List<CommandInfo>();
            for (var i = 0; i < gathered.Count; i++)
            {
                var (commandType, displayName, description) = gathered[i];

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
                    monograms[i],
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

        /// <summary>
        /// Derives the backlit-cap monogram for each command from its own name,
        /// deterministically and total over arbitrary input. Leading words shared by
        /// two or more sibling commands are dropped ("Show CPU usage" among other
        /// "Show" commands yields CU); a name fully consumed by the drop falls back
        /// to its undropped initials; an empty name falls back to the command type
        /// name's first character. No cap can render empty.
        /// </summary>
        private static List<string> DeriveMonograms(List<(Type CommandType, string DisplayName, string Description)> commands)
        {
            var tokenLists = commands
                .Select(c => (IReadOnlyList<string>)[.. c.DisplayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)])
                .ToList();

            // Iteratively drop leading tokens shared (case-insensitively) by two or
            // more names still holding tokens at that depth.
            var remaining = tokenLists.Select(t => t.ToList()).ToList();
            while (true)
            {
                var sharedLeads = remaining
                    .Where(t => t.Count > 0)
                    .GroupBy(t => t[0], StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() >= 2)
                    .Select(g => g.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (sharedLeads.Count == 0)
                {
                    break;
                }

                foreach (var tokens in remaining)
                {
                    if (tokens.Count > 0 && sharedLeads.Contains(tokens[0]))
                    {
                        tokens.RemoveAt(0);
                    }
                }
            }

            var monograms = new List<string>();
            for (var i = 0; i < commands.Count; i++)
            {
                var source = remaining[i].Count > 0 ? remaining[i] : [.. tokenLists[i]];
                var monogram = string.Concat(source.Take(2).Select(w => char.ToUpperInvariant(w[0])));
                if (monogram.Length == 0)
                {
                    monogram = char.ToUpperInvariant(commands[i].CommandType.Name[0]).ToString();
                }

                monograms.Add(monogram);
            }

            return monograms;
        }
    }
}
