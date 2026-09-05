using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Dispatches a session line through the command tree on a worker task
    /// while the footer shows the live state: an npm-style activity line with
    /// a spinner for ordinary commands (after a 150 ms grace period), or the
    /// listen status with a live event counter. Ctrl+C during a run is
    /// absorbed so the session survives.
    /// </summary>
    internal static class CommandRunner
    {
        internal static async Task RunAsync(RootCommand rootCommand, string line, FooterController footer)
        {
            var isListen = line.Equals("listen", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("listen ", StringComparison.OrdinalIgnoreCase);

            ConsoleCancelEventHandler absorb = (s, e) => e.Cancel = true;
            var previousTreatCtrlC = Console.TreatControlCAsInput;

            try
            {
                if (!isListen)
                {
                    Console.TreatControlCAsInput = false;
                    Console.CancelKeyPress += absorb;
                }

                var work = Task.Run(() => rootCommand.InvokeAsync(line));

                if (isListen)
                {
                    footer.EnterListen(ListenProfileName(line));
                    while (!work.IsCompleted)
                    {
                        footer.Tick();
                        await Task.Delay(120);
                    }
                }
                else
                {
                    var first = await Task.WhenAny(work, Task.Delay(150));
                    if (first != work)
                    {
                        footer.EnterRun($"running {FirstToken(line)}");
                        while (!work.IsCompleted)
                        {
                            footer.Tick();
                            await Task.Delay(80);
                        }
                    }
                }

                await work;
            }
            catch (Exception ex)
            {
                Output.Error(ex.InnerException?.Message ?? ex.Message);
            }
            finally
            {
                footer.Hide();
                if (!isListen)
                {
                    Console.CancelKeyPress -= absorb;
                    Console.TreatControlCAsInput = previousTreatCtrlC;
                }
            }
        }

        private static string FirstToken(string line)
        {
            var space = line.IndexOf(' ');
            return space < 0 ? line : line.Substring(0, space);
        }

        private static string ListenProfileName(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] is "-p" or "--profile")
                {
                    return parts[i + 1];
                }
            }

            return parts.Length > 1 ? parts[^1] : string.Empty;
        }
    }
}
