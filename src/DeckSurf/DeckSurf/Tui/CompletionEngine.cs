using DeckSurf.SDK.Core;
using DeckSurf.SDK.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Deterministic completion sources for the deck command surface: command
    /// phrases with descriptions for the slash palette, subcommands, option
    /// names, and dynamic values (profile names, device serials, plugin ids,
    /// command class names).
    /// </summary>
    internal sealed class CompletionEngine
    {
        private static readonly (string Phrase, string Detail)[] RootPhrases =
        [
            ("devices list", "show connected devices"),
            ("devices info", "inspect one device"),
            ("devices brightness", "set device brightness"),
            ("plugins list", "show loaded plugins and commands"),
            ("profiles list", "show all saved profiles"),
            ("profiles show", "inspect one profile"),
            ("profiles delete", "remove a profile"),
            ("write", "map a key to a plugin command"),
            ("listen", "activate a profile and stream events"),
            ("help", "show command help"),
            ("clear", "clear the screen"),
            ("exit", "leave the session"),
        ];

        private static readonly (string Name, string Detail)[] WriteOptions =
        [
            ("-d", "device index"),
            ("-s", "device serial"),
            ("-k", "key index"),
            ("-n", "plugin id"),
            ("-c", "command class name"),
            ("-i", "button image path"),
            ("-a", "key=value arguments"),
            ("-p", "profile name"),
        ];

        private List<string> pluginIds;
        private List<string> commandNames;
        private List<string> serialCache;
        private DateTime serialCacheAt = DateTime.MinValue;

        internal sealed class Item
        {
            public string Insert { get; set; }

            public string Label { get; set; }

            public string Detail { get; set; }
        }

        public List<Item> Complete(string buffer, int cursor, out int tokenStart)
        {
            var text = buffer.Substring(0, cursor);
            var slash = text.StartsWith("/", StringComparison.Ordinal);
            var body = slash ? text.Substring(1) : text;
            var bodyOffset = slash ? 1 : 0;

            var lastSpace = body.LastIndexOf(' ');
            tokenStart = bodyOffset + lastSpace + 1;
            var current = body.Substring(lastSpace + 1);
            var before = lastSpace < 0 ? string.Empty : body.Substring(0, lastSpace);
            var tokens = before.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var candidates = this.Candidates(tokens);
            return candidates
                .Where(c => c.Insert.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static List<Item> Plain(IEnumerable<string> values)
        {
            return values.Select(v => new Item { Insert = v, Label = v }).ToList();
        }

        private static List<string> ProfileNames()
        {
            try
            {
                return ConfigurationHelper.ListProfiles().ToList();
            }
            catch (Exception)
            {
                return [];
            }
        }

        private List<Item> Candidates(string[] tokens)
        {
            if (tokens.Length == 0)
            {
                return RootPhrases
                    .Select(p => new Item { Insert = p.Phrase, Label = p.Phrase, Detail = p.Detail })
                    .ToList();
            }

            switch (tokens[0].ToLowerInvariant())
            {
                case "devices":
                    if (tokens.Length == 1)
                    {
                        return
                        [
                            new Item { Insert = "list", Label = "list", Detail = "show connected devices" },
                            new Item { Insert = "info", Label = "info", Detail = "inspect one device" },
                            new Item { Insert = "brightness", Label = "brightness", Detail = "set device brightness" },
                        ];
                    }

                    return [];

                case "plugins":
                    if (tokens.Length == 1)
                    {
                        return [new Item { Insert = "list", Label = "list", Detail = "show loaded plugins and commands" }];
                    }

                    if (tokens.Length == 2 && string.Equals(tokens[1], "list", StringComparison.OrdinalIgnoreCase))
                    {
                        return [new Item { Insert = "--full", Label = "--full", Detail = "include command settings" }];
                    }

                    return [];

                case "profiles":
                    if (tokens.Length == 1)
                    {
                        return
                        [
                            new Item { Insert = "list", Label = "list", Detail = "show all saved profiles" },
                            new Item { Insert = "show", Label = "show", Detail = "inspect one profile" },
                            new Item { Insert = "delete", Label = "delete", Detail = "remove a profile" },
                        ];
                    }

                    if (tokens.Length == 2 && (string.Equals(tokens[1], "show", StringComparison.OrdinalIgnoreCase) || string.Equals(tokens[1], "delete", StringComparison.OrdinalIgnoreCase)))
                    {
                        return Plain(ProfileNames());
                    }

                    if (tokens.Length == 3 && string.Equals(tokens[1], "show", StringComparison.OrdinalIgnoreCase))
                    {
                        return [new Item { Insert = "--json", Label = "--json", Detail = "print the raw profile" }];
                    }

                    return [];

                case "listen":
                    if (tokens[^1] is "-p" or "--profile" || tokens.Length == 1)
                    {
                        return Plain(ProfileNames());
                    }

                    return [];

                case "write":
                    switch (tokens[^1].ToLowerInvariant())
                    {
                        case "-n":
                        case "--plugin":
                            return Plain(this.PluginIds());
                        case "-c":
                        case "--command":
                            return Plain(this.CommandNames());
                        case "-p":
                        case "--profile":
                            return Plain(ProfileNames());
                        case "-s":
                        case "--device-serial":
                            return Plain(this.Serials());
                        case "-d":
                        case "--device-index":
                        case "-k":
                        case "--key-index":
                        case "-i":
                        case "--image-path":
                        case "-a":
                        case "--action-args":
                            return [];
                        default:
                            return WriteOptions
                                .Select(o => new Item { Insert = o.Name, Label = o.Name, Detail = o.Detail })
                                .ToList();
                    }

                default:
                    return [];
            }
        }

        private List<string> PluginIds()
        {
            this.EnsurePlugins();
            return this.pluginIds;
        }

        private List<string> CommandNames()
        {
            this.EnsurePlugins();
            return this.commandNames;
        }

        private List<string> Serials()
        {
            if ((DateTime.UtcNow - this.serialCacheAt).TotalSeconds > 3)
            {
                try
                {
                    this.serialCache = DeviceManager.GetDeviceList()
                        .Select(d => d.Serial)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }
                catch (Exception)
                {
                    this.serialCache = [];
                }

                this.serialCacheAt = DateTime.UtcNow;
            }

            return this.serialCache;
        }

        private void EnsurePlugins()
        {
            if (this.pluginIds != null)
            {
                return;
            }

            try
            {
                var plugins = PluginLoader.LoadPlugins(AppContext.BaseDirectory, _ => { });
                this.pluginIds = plugins.Select(p => p.Metadata.Id).ToList();
                this.commandNames = plugins
                    .SelectMany(p => p.GetSupportedCommands())
                    .Select(t => t.Name)
                    .Distinct()
                    .ToList();
            }
            catch (Exception)
            {
                this.pluginIds = [];
                this.commandNames = [];
            }
        }
    }

    /// <summary>
    /// State of the open completion menu: items, selection, the token span the
    /// accepted item replaces, and the visible window.
    /// </summary>
    internal sealed class CompletionMenu
    {
        internal const int MaxVisible = 6;

        public List<CompletionEngine.Item> Items { get; set; }

        public int Selected { get; set; }

        public int TokenStart { get; set; }

        public int WindowStart { get; set; }

        public void MoveUp()
        {
            this.Selected = this.Selected == 0 ? this.Items.Count - 1 : this.Selected - 1;
            this.FollowSelection();
        }

        public void MoveDown()
        {
            this.Selected = this.Selected == this.Items.Count - 1 ? 0 : this.Selected + 1;
            this.FollowSelection();
        }

        private void FollowSelection()
        {
            if (this.Selected < this.WindowStart)
            {
                this.WindowStart = this.Selected;
            }
            else if (this.Selected >= this.WindowStart + MaxVisible)
            {
                this.WindowStart = this.Selected - MaxVisible + 1;
            }
        }
    }
}
