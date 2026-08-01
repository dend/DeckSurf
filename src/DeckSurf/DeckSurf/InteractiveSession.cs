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
    /// Prompt-driven session started when 'deck' runs bare on a real terminal.
    /// Lines typed at the prompt are dispatched through the same command tree
    /// as one-shot invocations. On terminals that support it, input runs
    /// through the rich line editor with history and completion; redirected
    /// sessions fall back to a plain ReadLine loop.
    /// </summary>
    internal static class InteractiveSession
    {
        private static readonly string[] KnownCommands = ["devices", "plugins", "profiles", "write", "listen"];

        internal static async Task<int> RunAsync(RootCommand rootCommand)
        {
            var richInput = TerminalCapabilities.SupportsRichInput;
            ConsoleGate gate = null;
            if (richInput)
            {
                gate = new ConsoleGate(Console.Out);
                Console.SetOut(gate);
            }

            PrintBanner();
            Console.WriteLine();

            if (!richInput)
            {
                return await LegacyLoopAsync(rootCommand);
            }

            var editor = new LineEditor(new CompletionEngine(), () =>
            {
                PrintBanner();
                Console.WriteLine();
            });

            var startedAt = DateTime.UtcNow;
            var commandCount = 0;

            while (true)
            {
                var line = editor.ReadLine();
                if (line == null)
                {
                    break;
                }

                line = Normalize(line);
                if (line == null)
                {
                    continue;
                }

                if (line == "exit")
                {
                    break;
                }

                if (line == "clear")
                {
                    AnsiConsole.Clear();
                    PrintBanner();
                    Console.WriteLine();
                    continue;
                }

                if (!PreflightParse(rootCommand, line))
                {
                    Console.WriteLine();
                    continue;
                }

                commandCount++;
                await CommandRunner.RunAsync(rootCommand, line, gate);
                Console.WriteLine();
            }

            Output.SessionClosed(commandCount, DateTime.UtcNow - startedAt);
            return 0;
        }

        /// <summary>
        /// Strips the optional slash, expands session verbs and shorthands.
        /// Returns null when the line is empty, "exit" or "clear" for session
        /// actions, or the dispatchable command line.
        /// </summary>
        private static string Normalize(string line)
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

        /// <summary>
        /// Renders parse failures in the session's own voice instead of the
        /// System.CommandLine default, with a did-you-mean for near misses.
        /// </summary>
        private static bool PreflightParse(RootCommand rootCommand, string line)
        {
            var parsed = rootCommand.Parse(line);
            if (parsed.Errors.Count == 0)
            {
                return true;
            }

            var firstToken = line.Split(' ')[0];
            var suggestion = NearestCommand(firstToken);
            if (suggestion != null)
            {
                Output.Error($"unknown command: {firstToken}.");
                Output.Hint($"Did you mean {suggestion}?", "/ lists all commands.");
            }
            else
            {
                Output.Error(parsed.Errors[0].Message);
                Output.Line("/ lists all commands. help shows usage.");
            }

            return false;
        }

        private static string NearestCommand(string token)
        {
            foreach (var command in KnownCommands)
            {
                var distance = Distance(token.ToLowerInvariant(), command);
                if (distance > 0 && distance <= 2 && token.Length > 2)
                {
                    return command;
                }
            }

            return null;
        }

        private static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (var i = 0; i <= a.Length; i++)
            {
                d[i, 0] = i;
            }

            for (var j = 0; j <= b.Length; j++)
            {
                d[0, j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[a.Length, b.Length];
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
                    PrintBanner();
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

        private static void PrintBanner()
        {
            Output.Banner(GetDisplayVersion(), SafeDevices());
        }

        private static IReadOnlyList<ConnectedDevice> SafeDevices()
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

        private static string GetDisplayVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            var metadataStart = version.IndexOf('+');
            return metadataStart > 0 ? version.Substring(0, metadataStart) : version;
        }
    }
}
