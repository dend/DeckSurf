using DeckSurf.SDK.Core;
using Spectre.Console;
using System;
using System.CommandLine;
using System.Reflection;
using System.Threading.Tasks;

namespace DeckSurf
{
    /// <summary>
    /// Prompt-driven session started when 'deck' runs bare on a real terminal.
    /// Lines typed at the prompt are dispatched through the same command tree
    /// as one-shot invocations, so everything scriptable works here too.
    /// </summary>
    internal static class InteractiveSession
    {
        internal static async Task<int> RunAsync(RootCommand rootCommand)
        {
            Output.Banner(GetDisplayVersion(), GetDeviceCountSafe());
            Console.WriteLine();

            while (true)
            {
                AnsiConsole.Markup("[bold cyan]deck>[/] ");
                var line = Console.ReadLine();
                if (line == null)
                {
                    return 0;
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                switch (line.ToLowerInvariant())
                {
                    case "exit":
                    case "quit":
                    case "q":
                        return 0;
                    case "clear":
                    case "cls":
                        AnsiConsole.Clear();
                        continue;
                    case "help":
                    case "?":
                        line = "--help";
                        break;
                    case "devices":
                        line = "devices list";
                        break;
                    case "plugins":
                        line = "plugins list";
                        break;
                    case "profiles":
                        line = "profiles list";
                        break;
                }

                // 'listen obs-test' reads naturally at a prompt; expand it to the
                // canonical option form before dispatch.
                if (line.StartsWith("listen ", StringComparison.OrdinalIgnoreCase) && !line.Contains(" -", StringComparison.Ordinal))
                {
                    line = "listen -p " + line.Substring("listen ".Length).Trim();
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

        private static string GetDisplayVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            var metadataStart = version.IndexOf('+');
            return metadataStart > 0 ? version.Substring(0, metadataStart) : version;
        }

        private static int GetDeviceCountSafe()
        {
            try
            {
                return DeviceManager.GetDeviceList().Count;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
