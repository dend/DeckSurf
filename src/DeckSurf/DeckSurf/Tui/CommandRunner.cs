using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Dispatches a session line through the command tree on a worker task,
    /// animating a spinner if the command is still running after a 150 ms
    /// grace period. Ctrl+C during a run is absorbed so the session survives.
    /// </summary>
    internal static class CommandRunner
    {
        internal static async Task RunAsync(RootCommand rootCommand, string line, ConsoleGate gate)
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

                if (!isListen)
                {
                    var first = await Task.WhenAny(work, Task.Delay(150));
                    if (first != work)
                    {
                        using var spinner = new Spinner(gate, $"running {FirstToken(line)}");
                        while (!work.IsCompleted)
                        {
                            spinner.Frame();
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
    }
}
