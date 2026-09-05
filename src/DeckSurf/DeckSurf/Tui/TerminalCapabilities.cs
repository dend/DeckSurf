using System;
using System.Runtime.InteropServices;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Decides once whether the terminal can host the rich interactive editor,
    /// and provides safe terminal metrics.
    /// </summary>
    internal static class TerminalCapabilities
    {
        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        private static bool? richInput;

        internal static bool SupportsRichInput
        {
            get
            {
                richInput ??= Compute();
                return richInput.Value;
            }
        }

        internal static int SafeWindowWidth()
        {
            try
            {
                return Math.Max(20, Console.WindowWidth);
            }
            catch (Exception)
            {
                return 80;
            }
        }

        private static bool Compute()
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                return false;
            }

            return EnableVt();
        }

        private static bool EnableVt()
        {
            if (!OperatingSystem.IsWindows())
            {
                return true;
            }

            try
            {
                var handle = GetStdHandle(StdOutputHandle);
                if (!GetConsoleMode(handle, out var mode))
                {
                    return false;
                }

                if ((mode & EnableVirtualTerminalProcessing) != 0)
                {
                    return true;
                }

                return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
