using DeckSurf.SDK.Core;
using DeckSurf.SDK.Models;
using DeckSurf.Tui;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Reflection;
using System.Threading.Tasks;

namespace DeckSurf
{
    /// <summary>
    /// Entry point for the interactive session. On a capable terminal the
    /// session runs as a Terminal.Gui application (scrolling transcript,
    /// anchored input with autocomplete, status bar); redirected sessions fall
    /// back to a plain ReadLine loop over the same command tree.
    /// </summary>
    internal static class InteractiveSession
    {
        internal static RootCommand SharedRootCommand { get; private set; }

        internal static async Task<int> RunAsync(RootCommand rootCommand)
        {
            SharedRootCommand = rootCommand;

            if (TerminalCapabilities.SupportsRichInput)
            {
                return GuiSession.Run(rootCommand, GetDisplayVersion());
            }

            Output.Banner(GetDisplayVersion(), SafeDevices());
            Console.WriteLine();
            return await LegacyLoopAsync(rootCommand);
        }

        /// <summary>
        /// Strips the optional slash, expands session verbs and shorthands.
        /// Returns null when the line is empty, "exit" or "clear" for session
        /// actions, or the dispatchable command line.
        /// </summary>
        internal static string Normalize(string line)
        {
            line = line.Trim();
            if (line.StartsWith("/", StringComparison.Ordinal))
            {
                line = line.Substring(1).Trim();
            }

            if (line.Length == 0)
            {
                return null;
            }

            switch (line.ToLowerInvariant())
            {
                case "exit":
                case "quit":
                case "q":
                    return "exit";
                case "clear":
                case "cls":
                    return "clear";
                case "help":
                case "?":
                    return "--help";
                case "devices":
                    return "devices list";
                case "plugins":
                    return "plugins list";
                case "profiles":
                    return "profiles list";
            }

            if (line.StartsWith("listen ", StringComparison.OrdinalIgnoreCase) && !line.Contains(" -", StringComparison.Ordinal))
            {
                return "listen -p " + line.Substring("listen ".Length).Trim();
            }

            return line;
        }

        internal static IReadOnlyList<ConnectedDevice> SafeDevices()
        {
            try
            {
                return DeviceManager.GetDeviceList();
            }
            catch (Exception)
            {
                return [];
            }
        }

        internal static string GetDisplayVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            var metadataStart = version.IndexOf('+');
            return metadataStart > 0 ? version.Substring(0, metadataStart) : version;
        }

        private static async Task<int> LegacyLoopAsync(RootCommand rootCommand)
        {
            while (true)
            {
                AnsiConsole.Markup("[bold]>[/] ");
                var line = Console.ReadLine();
                if (line == null)
                {
                    return 0;
                }

                line = Normalize(line);
                if (line == null)
                {
                    continue;
                }

                if (line == "exit")
                {
                    return 0;
                }

                if (line == "clear")
                {
                    AnsiConsole.Clear();
                    Output.Banner(GetDisplayVersion(), SafeDevices());
                    Console.WriteLine();
                    continue;
                }

                try
                {
                    await rootCommand.InvokeAsync(line);
                }
                catch (Exception ex)
                {
                    Output.Error(ex.Message);
                }

                Console.WriteLine();
            }
        }
    }
}
